using System;
using System.Linq;
using System.Net;
using RestSharp;
using RestSharp.Deserializers;

namespace StockFilter
{
    public class BaseClient : RestClient
    {
        protected ICacheService _cache;
        protected IErrorLogger _errorLogger;

        public BaseClient(ICacheService cache, IDeserializer serializer, IErrorLogger errorLogger, string baseUrl)
        {
            _cache = cache;
            _errorLogger = errorLogger;
            AddHandler("application/json", serializer);
            AddHandler("text/json", serializer);
            AddHandler("text/x-json", serializer);
            BaseUrl = new Uri(baseUrl);
        }
        public BaseClient(IDeserializer serializer, IErrorLogger errorLogger, string baseUrl)
        {
            _errorLogger = errorLogger;
            AddHandler("application/json", serializer);
            AddHandler("text/json", serializer);
            AddHandler("text/x-json", serializer);
            BaseUrl = new Uri(baseUrl);
        }
        private void TimeoutCheck(IRestRequest request, IRestResponse response)
        {
            if (response.StatusCode == 0)
            {
                LogError(BaseUrl, request, response);
            }
        }

        private void LogError(Uri baseUrl, IRestRequest request, IRestResponse response)
        {
            //Get the values of parameters passed to the API
            string parameters = string.Join(", ", request.Parameters.Select(x => x.Name.ToString()));

            //Setup the information message with the URL, the status code, and the parameters
            string info = "Request to " + BaseUrl.AbsoluteUri + request.Resource + " failed with status code " + response.StatusCode + ", parameters: "
                          + parameters + ", and content: " + response.Content;

            //Accquire the actual exception
            Exception ex;
            if (response != null && response.ErrorException != null)
            {
                ex = response.ErrorException;
            }
            else
            {
                ex = new Exception(info);
                info = string.Empty;
            }

            //Log the exception and info messages
            _errorLogger.LogError(ex, info);
        }

        public override IRestResponse Execute(IRestRequest request)
        {
            var response = base.Execute(request);
            TimeoutCheck(request, response);
            return response;
        }

        public override IRestResponse<T> Execute<T>(IRestRequest request)
        {
            var response = base.Execute<T>(request);
            TimeoutCheck(request, response);
            return response;
        }

        public T Get<T>(IRestRequest request) where T : new()
        {
            var reponse = Execute<T>(request);
            var content = reponse.Content;
            if (reponse.StatusCode == HttpStatusCode.OK)
            {
                return reponse.Data;
            }
            else
            {
                LogError(BaseUrl, request, reponse);
                return default(T);
            }
        }

        public T Get<T>(IRestRequest request, out string content) where T : new()
        {
            var reponse = Execute<T>(request);
            content = reponse.Content;
            if (reponse.StatusCode == HttpStatusCode.OK)
            {
                return reponse.Data;
            }
            else
            {
                LogError(BaseUrl, request, reponse);
                return default(T);
            }
        }

        public T GetFromCache<T>(IRestRequest request, string cacheKey)
                    where T : class, new()
        {
            var item = _cache.Get<T>(cacheKey);
            if (item == null) //if the cache doen't have the item
            {
                var response = Execute<T>(request);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    _cache.Set(cacheKey, response.Data);
                    item = response.Data;
                }
                else
                {
                    LogError(BaseUrl, request, response);
                    return default(T);
                }
            }

            return item;
        }


    }
}
