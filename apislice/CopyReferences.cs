using Microsoft.OpenApi.Interfaces;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace apislice
{
    public class CopyReferences : OpenApiVisitorBase
    {
        private readonly OpenApiDocument source;
        private readonly OpenApiDocument target;
        public OpenApiComponents Components = new OpenApiComponents();

        public CopyReferences(OpenApiDocument source, OpenApiDocument target)
        {
            this.source = source;
            this.target = target;
        }

        public override void Visit(IOpenApiReferenceable referenceable)
        {
            switch (referenceable)
            {
                case OpenApiSchema schema:
                    EnsureComponentsExists();
                    EnsureSchemasExists();
                    if (!Components.Schemas.ContainsKey(schema.Reference.Id))
                    {
                        Components.Schemas.Add(schema.Reference.Id, schema);
                    }
                    break;

                case OpenApiParameter parameter:
                    EnsureComponentsExists();
                    EnsureParametersExists();
                    if (!Components.Parameters.ContainsKey(parameter.Reference.Id))
                    {
                        Components.Parameters.Add(parameter.Reference.Id, parameter);
                    }
                    break;

                case OpenApiResponse response:
                    EnsureComponentsExists();
                    EnsureResponsesExists();
                    if (!Components.Responses.ContainsKey(response.Reference.Id))
                    {
                        Components.Responses.Add(response.Reference.Id, response);
                    }
                    break;

                default:
                    break;
            }
            base.Visit(referenceable);
        }

        public override void Visit(OpenApiSchema schema)
        {
            // This is needed to handle schemas used in Responses in components
            if (schema.Reference != null)
            {
                EnsureComponentsExists();
                EnsureSchemasExists();
                if (!Components.Schemas.ContainsKey(schema.Reference.Id))
                {
                    Components.Schemas.Add(schema.Reference.Id, schema); 
                }
            }
            base.Visit(schema);
        }

        private void EnsureComponentsExists()
        {
            if (target.Components == null)
            {
                target.Components = new OpenApiComponents();
            }
        }

        private void EnsureSchemasExists()
        {
            if (target.Components.Schemas == null)
            {
                target.Components.Schemas = new Dictionary<string, OpenApiSchema>();
            }
        }

        private void EnsureParametersExists()
        {
            if (target.Components.Parameters == null)
            {
                target.Components.Parameters = new Dictionary<string, OpenApiParameter>();
            }
        }

        private void EnsureResponsesExists()
        {
            if (target.Components.Responses == null)
            {
                target.Components.Responses = new Dictionary<string, OpenApiResponse>();
            }
        }
    }

    public class AnyOfRemover : OpenApiVisitorBase
    {
        public void visitProperties(IDictionary<string, OpenApiSchema> properties)
        {
            foreach (var property in properties)
            {
                if (property.Value != null)
                {
                    var currentSchema = property.Value;

                    if (currentSchema.Properties != null && currentSchema.Properties.Count > 0)
                    {
                        visitProperties(currentSchema.Properties);
                        continue;
                    }

                    if (currentSchema.AnyOf != null && currentSchema.AnyOf.Count > 0)
                    {
                        var curr = currentSchema.AnyOf.FirstOrDefault();
                        currentSchema.AnyOf = null;

                        if (curr.Reference != null)
                        {
                            currentSchema.Reference = curr.Reference;
                        }
                        else
                        {
                            currentSchema.Type = curr.Type;
                        }
                    }
                    if (currentSchema.Items != null)
                    {
                        if (currentSchema.Items.AnyOf != null && currentSchema.Items.AnyOf.Count > 0)
                        {
                            var curr = currentSchema.Items.AnyOf.FirstOrDefault();
                            currentSchema.Items.AnyOf = null;

                            if (curr.Reference != null)
                            {
                                currentSchema.Items.Reference = curr.Reference;
                            }
                            else
                            {
                                currentSchema.Items.Type = curr.Type;
                            }
                        }
                    }
                }
            }
        }

        public override void Visit(OpenApiSchema schema)
        {
            if (schema.AnyOf != null && schema.AnyOf.Count > 0)
            {
                var newSchema = schema.AnyOf.FirstOrDefault();

                if (newSchema != null)
                {
                    if (newSchema.AllOf != null && newSchema.AllOf.Count > 0)
                    {
                        foreach (var abc in newSchema.AllOf)
                        {
                            Visit(abc);
                        }
                    }

                    if (newSchema.Properties != null && newSchema.Properties.Count > 0)
                    {
                        visitProperties(newSchema.Properties);
                    }
                }
            }

            if (schema.Properties != null && schema.Properties.Count > 0)
            {
                visitProperties(schema.Properties);
            }
        }
    }
}
