using Microsoft.OpenApi.Interfaces;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Microsoft.OpenApi.Services;
using Microsoft.OpenApi.Validations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;

namespace apislice
{

    public class FilterOpenApiService
    {
        const string graphV1OpenApiUrl = "https://github.com/microsoftgraph/microsoft-graph-openapi/blob/master/v1.0.json?raw=true";
        const string graphBetaOpenApiUrl = "https://github.com/microsoftgraph/microsoft-graph-openapi/blob/master/beta.json?raw=true";
        static OpenApiDocument _OpenApiV1Document;
        static OpenApiDocument _OpenApiBetaDocument;

        private static IList<SearchResult> FindOperations(OpenApiDocument graphOpenApi, Func<OpenApiOperation, bool> predicate)
        {
            var search = new OperationSearch(predicate);
            var walker = new OpenApiWalker(search);
            walker.Walk(graphOpenApi);
            return search.SearchResults;
        }

        public static OpenApiDocument CreateFilteredDocument(OpenApiDocument source, Func<OpenApiOperation, bool> predicate)
        {
            var subset = new OpenApiDocument();
            subset.Info = new OpenApiInfo()
            {
                Title = "Subset of Microsoft Graph API",
                Version = ""
            };

            subset.Components = new OpenApiComponents();
            var aadv2Scheme = new OpenApiSecurityScheme()
            {
                Type = SecuritySchemeType.OAuth2,
                Flows = new OpenApiOAuthFlows()
                {
                    AuthorizationCode = new OpenApiOAuthFlow()
                    {
                        AuthorizationUrl = new Uri("https://login.microsoftonline.com/common/oauth2/v2.0/authorize"),
                        TokenUrl = new Uri("https://login.microsoftonline.com/common/oauth2/v2.0/token")
                    }
                },
                Reference = new OpenApiReference() { Id = "azureaadv2", Type = ReferenceType.SecurityScheme },
                UnresolvedReference = false
            };
            subset.Components.SecuritySchemes.Add("azureaadv2", aadv2Scheme);

            subset.SecurityRequirements.Add(new OpenApiSecurityRequirement() { { aadv2Scheme, new string[] { } } });
            
            subset.Servers.Add(new OpenApiServer() { Description = "Core", Url = "https://graph.microsoft.com/v1.0/" });

            var operationObjects = new List<OpenApiOperation>();
            var results = FindOperations(source, predicate);
            foreach (var result in results)
            {
                OpenApiPathItem pathItem = null;
                if (subset.Paths == null)
                {
                    subset.Paths = new OpenApiPaths();
                    pathItem = new OpenApiPathItem();
                    subset.Paths.Add(result.CurrentKeys.Path, pathItem);
                }
                else
                {
                    if (!subset.Paths.TryGetValue(result.CurrentKeys.Path, out pathItem))
                    {
                        pathItem = new OpenApiPathItem();
                        subset.Paths.Add(result.CurrentKeys.Path, pathItem);
                    }
                }

                pathItem.Operations.Add((OperationType)result.CurrentKeys.Operation, result.Operation);
              //  CopyRefs(subset, result.Operation);
            }
            return subset;
        }

        private static void CopyRefs(OpenApiDocument subset, OpenApiOperation operation)
        {
            subset.Components = new OpenApiComponents();

            foreach (var parameter in operation.Parameters)
            {
                if (parameter.Reference != null)
                {
                    if (subset.Components.Parameters == null) {
                        subset.Components.Parameters = new Dictionary<string, OpenApiParameter>();
                    }
                    subset.Components.Parameters.Add(parameter.Reference.Id, parameter);
                }
            }

            foreach (var response in operation.Responses.Values)
            {
                if (response.Reference != null)
                {
                    if (subset.Components.Responses == null)
                    {
                        subset.Components.Responses = new Dictionary<string, OpenApiResponse>();
                    }
                    subset.Components.Responses.Add(response.Reference.Id, response);
                }
                else
                {
                    foreach (var item in response.Content.Values)
                    {
                        if (item.Schema != null && item.Schema.Reference != null)
                        {
                            if (subset.Components.Schemas == null)
                            {
                                subset.Components.Schemas = new Dictionary<string, OpenApiSchema>();
                            }
                            subset.Components.Schemas.Add(item.Schema.Reference.Id, item.Schema);
                        }

                    }
                }
            }
        }

        public static OpenApiDocument GetGraphOpenApiV1()
        {
            if (_OpenApiV1Document != null)
            {
                return _OpenApiV1Document;
            }

            _OpenApiV1Document = GetOpenApiDocument(graphV1OpenApiUrl);

            return _OpenApiV1Document;
        }

        public static OpenApiDocument GetGraphOpenApiBeta()
        {
            if (_OpenApiBetaDocument != null)
            {
                return _OpenApiBetaDocument;
            }

            _OpenApiBetaDocument = GetOpenApiDocument(graphBetaOpenApiUrl);

            return _OpenApiBetaDocument;
        }

        private static OpenApiDocument GetOpenApiDocument(string url)
        {
            HttpClient httpClient = CreateHttpClient();

            var response = httpClient.GetAsync(url)
                                .GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Failed to retrieve OpenApi document");
            }

            var stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult(); ;

            var newrules = ValidationRuleSet.GetDefaultRuleSet().Rules
                .Where(r => r.GetType() != typeof(ValidationRule<OpenApiSchema>)).ToList();
            

            var reader = new OpenApiStreamReader(new OpenApiReaderSettings() {
                RuleSet = new ValidationRuleSet(newrules)
            });
            var openApiDoc = reader.Read(stream, out var diagnostic);

            if (diagnostic.Errors.Count > 0)
            {
                throw new Exception("OpenApi document has errors : " + String.Join("\n", diagnostic.Errors));
            }
            return openApiDoc;
        }

        private static HttpClient CreateHttpClient()
        {
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var httpClient = new HttpClient(new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip
            });
            httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("gzip"));
            httpClient.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("apislice", "1.0"));
            return httpClient;
        }

        public static void CopyReferences(OpenApiDocument source, OpenApiDocument target)
        {
            bool morestuff = false;
            do
            {
                var copy = new CopyReferences(source, target);
                var walker = new OpenApiWalker(copy);
                walker.Walk(target);

                morestuff = Add(copy.Components, target.Components);
                
            } while (morestuff);

        }

        private static bool Add(OpenApiComponents newComponents, OpenApiComponents target)
        {
            var moreStuff = false; 
            foreach (var item in newComponents.Schemas)
            {
                if (!target.Schemas.ContainsKey(item.Key))
                {
                    moreStuff = true;
                    target.Schemas.Add(item);

                }
            }

            foreach (var item in newComponents.Parameters)
            {
                if (!target.Parameters.ContainsKey(item.Key))
                {
                    moreStuff = true;
                    target.Parameters.Add(item);
                }
            }

            foreach (var item in newComponents.Responses)
            {
                if (!target.Responses.ContainsKey(item.Key))
                {
                    moreStuff = true;
                    target.Responses.Add(item);
                }
            }

            return moreStuff;
        }

   }


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

    public class OpenApiOperationIndex : OpenApiVisitorBase
    {
        public Dictionary<OpenApiTag, List<OpenApiOperation>> Index = new Dictionary<OpenApiTag, List<OpenApiOperation>>();
        public override void Visit(OpenApiOperation operation)
        {
            foreach (var tag in operation.Tags)
            {
                AddToIndex(tag, operation);
            }
        }

        private void AddToIndex(OpenApiTag tag, OpenApiOperation operation)
        {
            List<OpenApiOperation> operations;
            if (!Index.TryGetValue(tag, out operations))
            {
                operations = new List<OpenApiOperation>();
                Index[tag] = operations;
            }

            operations.Add(operation);

        }
    }

    public class OperationSearch : OpenApiVisitorBase
    {
        private readonly Func<OpenApiOperation, bool> _predicate;

        private List<SearchResult> _searchResults = new List<SearchResult>();

        public IList<SearchResult> SearchResults { get { return _searchResults; } }

        public OperationSearch(Func<OpenApiOperation, bool> predicate)
        {
            this._predicate = predicate;
        }

        public override void Visit(OpenApiOperation operation)
        {
            if (_predicate(operation))
            {
                _searchResults.Add(new SearchResult()
                {
                    Operation = operation,
                    CurrentKeys = CopyCurrentKeys(CurrentKeys)
                });
            }
        }

        private CurrentKeys CopyCurrentKeys(CurrentKeys currentKeys)
        {
            var keys = new CurrentKeys
            {
                Path = currentKeys.Path,
                Operation = currentKeys.Operation
            };

            return keys;
        }
    }
    public class SearchResult
    {
        public CurrentKeys CurrentKeys { get; set; }
        public OpenApiOperation Operation { get; set; }
    }

}
