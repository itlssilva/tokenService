using Newtonsoft.Json;
using Polly;
using ServiceStack.Redis;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace TokenService
{
    public class Token
    {
        private AccessToken _accessToken = new AccessToken();

        public async Task<string> ObterTokenAsync()
        {
            if (HealthCheck())
            {
                if (!VerificarTokenExiste()) return _accessToken.access_token;
                try
                {
                    await ObterTokenParceiro();
                    GravarToken(_accessToken.access_token, _accessToken.expires_in);
                }
                catch (Exception)
                {
                    throw new Exception("Não foi possível obter o Token!");
                }
                return _accessToken.access_token;
            }
            else
            {
                await ObterTokenParceiro();
                return _accessToken.access_token;
            }
        }

        private async Task ObterTokenParceiro()
        {
            var policy = Policy.Handle<Exception>(e =>
                {
                    Console.WriteLine("Tentando obter Token");
                    return true;
                }).WaitAndRetryAsync(3, i => TimeSpan.FromTicks(5));

            await policy.ExecuteAsync(async () =>
            {
                await RetornarToken();
            });
        }

        private async Task RetornarToken()
        {
            string hostIdentityServer = "http://gmci.bandeiranteslog.com.br:3010/api/oauth/token";
            string usuario = "btp";
            string senha = "BR@51lT3rMiN4L";
            _accessToken = ObterTokenPorUsuario(hostIdentityServer, usuario, senha);
        }

        private AccessToken ObterTokenPorUsuario(string hostIdentityServer, string usuario, string senha)
        {
            using (var client = new HttpClient())
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                //Define Headers
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                //client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", "YnRwOkJSQDUxbFQzck1pTjRMWW5KaGMybHNkR1Z5YldsdVlXdz0=");

                //Prepare Request Body
                List<KeyValuePair<string, string>> requestData = new List<KeyValuePair<string, string>>();

                requestData.Add(new KeyValuePair<string, string>("username", usuario));
                requestData.Add(new KeyValuePair<string, string>("password", senha));
                requestData.Add(new KeyValuePair<string, string>("grant_type", "password"));

                FormUrlEncodedContent requestBody = new FormUrlEncodedContent(requestData);

                //Request Token
                var request = client.PostAsync(hostIdentityServer, requestBody).Result;
                request.EnsureSuccessStatusCode();
                var token = request.Content.ReadAsStringAsync();                
                _accessToken = JsonConvert.DeserializeObject<AccessToken>(token.Result);
                //return accessToken.access_token;
                return _accessToken;
            }         
        }

        public class AccessToken
        {
            [JsonProperty(PropertyName = "AccessToken")]
            public string access_token { get; set; }

            [JsonProperty(PropertyName = "accessTokenExpiresAt")]
            public string expires_in { get; set; }
            public string token_type { get; set; }

        }

        private bool VerificarTokenExiste()
        {
            using (ConnectionMultiplexer connectionRedis = ConnectionMultiplexer.Connect("localhost:6379"))
            {
                IDatabase clientRedis = connectionRedis.GetDatabase();
                _accessToken.access_token = clientRedis.StringGet("038");
                var tempoToken = clientRedis.KeyTimeToLive("038");
                return string.IsNullOrEmpty(_accessToken.access_token);
            }
        }

        private void GravarToken(string token, string expireTime)
        {
            using (ConnectionMultiplexer connectionRedis = ConnectionMultiplexer.Connect("localhost:6379"))
            {
                IDatabase clientRedis = connectionRedis.GetDatabase();
                clientRedis.StringSet("038", token);
                //Console.WriteLine(clientRedis.StringGet("038"));
                clientRedis.KeyExpire("038", TimeSpan.FromSeconds(ConverterTempo(expireTime)));
                //Console.WriteLine(clientRedis.KeyTimeToLive("admin_sistema"));
                connectionRedis.Close();
            }
        }

        private double ConverterTempo(string expireTime)
        {
            var _expiresAt = Convert.ToDateTime(expireTime).AddMinutes(-10);
            return _expiresAt.Subtract(DateTime.Now).TotalSeconds;
        }

        private bool HealthCheck()
        {
            using (var client = new RedisClient("localhost", 6379))
            {
                try
                {
                    var response = client.Info;
                    return response != null && response.Any();
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }
    }
}
