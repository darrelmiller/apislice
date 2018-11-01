using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Microsoft.OpenApi.Services;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;

namespace apislice
{

    public class FilterOpenApiService
    {
        const string graphV1OpenApiUrl = "https://github.com/microsoftgraph/microsoft-graph-openapi/blob/master/v1.0.json?raw=true";
        static OpenApiDocument _OpenApiV1Document;

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
                CopyRefs(subset, result.Operation);
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

            HttpClient httpClient = CreateHttpClient();

            var response = httpClient.GetAsync(graphV1OpenApiUrl)
                                .GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Failed to retrieve OpenApi document");
            }

            var stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult(); ;

            var reader = new OpenApiStreamReader();
            _OpenApiV1Document = reader.Read(stream, out var diagnostic);

            if (diagnostic.Errors.Count > 0)
            {
                throw new Exception("OpenApi document has errors : " + String.Join("\n", diagnostic.Errors));
            }

            return _OpenApiV1Document;
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
            var copy = new CopyReferences(source,target);
            var walker = new OpenApiWalker(copy);
            walker.Walk(target);
        }
    }


    public class CopyReferences : OpenApiVisitorBase
    {
        private readonly OpenApiDocument source;
        private readonly OpenApiDocument target;

        public CopyReferences(OpenApiDocument source, OpenApiDocument target)
        {
            this.source = source;
            this.target = target;
        }


        public override void Visit(OpenApiSchema schema)
        {
            if (schema.Reference != null )
            {
                if (target.Components == null)
                {
                    target.Components = new OpenApiComponents();
                }

                if (target.Components.Schemas == null)
                {
                    target.Components.Schemas = new Dictionary<string, OpenApiSchema>();
                }
                if (!target.Components.Schemas.ContainsKey(schema.Reference.Id))
                {
                    target.Components.Schemas.Add(schema.Reference.Id, schema);
                }
            }
        }

        public override void Visit(OpenApiParameter parameter)
        {
            if (parameter.Reference != null )
            {
                if (target.Components == null)
                {
                    target.Components = new OpenApiComponents();
                }

                if (target.Components.Parameters == null)
                {
                    target.Components.Parameters = new Dictionary<string, OpenApiParameter>();
                }
                if (!target.Components.Parameters.ContainsKey(parameter.Reference.Id))
                {
                    target.Components.Parameters.Add(parameter.Reference.Id, parameter);
                }
            }
            base.Visit(parameter);
        }

        public override void Visit(OpenApiResponse response)
        {
            if (response.Reference != null )
            {
                if (target.Components == null)
                {
                    target.Components = new OpenApiComponents();
                }

                if (target.Components.Responses == null)
                {
                    target.Components.Responses = new Dictionary<string, OpenApiResponse>();
                }
                if (!target.Components.Responses.ContainsKey(response.Reference.Id))
                {
                    target.Components.Responses.Add(response.Reference.Id, response);
                }
            }
            base.Visit(response);
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
