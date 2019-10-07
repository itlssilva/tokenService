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

        public async Task<string> ObterTokenAsync(Tra tra)
        {
            if (HealthCheck())
            {
                if (!VerificarTokenExiste(tra)) return _accessToken.access_token;
                try
                {
                    await ObterTokenParceiro(tra);
                    GravarToken(_accessToken.access_token, _accessToken.expires_in, tra);
                }
                catch (Exception err)
                {
                    throw new Exception($"Não foi possível obter o Token! + {err.Message}");
                }
                return _accessToken.access_token;
            }
            else
            {
                await ObterTokenParceiro(tra);
                return _accessToken.access_token;
            }
        }

        private async Task ObterTokenParceiro(Tra tra)
        {
            var policy = Policy.Handle<Exception>(e =>
                {
                    Console.WriteLine("Tentando obter Token");
                    return true;
                }).WaitAndRetryAsync(3, i => TimeSpan.FromTicks(5));

            await policy.ExecuteAsync(async () =>
            {
                await RetornarToken(tra);
            });
        }

        private async Task RetornarToken(Tra tra)
        {
            string hostIdentityServer = tra.Url;
            string usuario = tra.Username;
            string senha = tra.Password;
            _accessToken = ObterTokenPorUsuario(hostIdentityServer, usuario, senha);
        }

        private AccessToken ObterTokenPorUsuario(string hostIdentityServer, string usuario, string senha)
        {
            using (var client = new HttpClient())
            {
                //ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
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
            [JsonProperty(PropertyName = "access_token")]
            public string access_token { get; set; }

            //[JsonProperty(PropertyName = "accessTokenExpiresAt")]
            public string expires_in { get; set; }
            public string token_type { get; set; }

        }

        private bool VerificarTokenExiste(Tra tra)
        {
            using (ConnectionMultiplexer connectionRedis = ConnectionMultiplexer.Connect("localhost:6379"))
            {
                IDatabase clientRedis = connectionRedis.GetDatabase();
                _accessToken.access_token = clientRedis.StringGet(tra.CodDte);
                var tempoToken = clientRedis.KeyTimeToLive(tra.CodDte);
                return string.IsNullOrEmpty(_accessToken.access_token);
            }
        }

        private void GravarToken(string token, string expireTime, Tra tra)
        {
            using (ConnectionMultiplexer connectionRedis = ConnectionMultiplexer.Connect("localhost:6379"))
            {
                IDatabase clientRedis = connectionRedis.GetDatabase();
                clientRedis.StringSet(tra.CodDte, token);
                clientRedis.KeyExpire(tra.CodDte, TimeSpan.FromSeconds(ConverterTempo(expireTime)));
                connectionRedis.Close();
            }
        }

        private double ConverterTempo(string expireTime)
        {
            if (string.IsNullOrEmpty(expireTime))
                expireTime = "10800";

            DateTime _expiresAt;
            DateTime date;

            _expiresAt = DateTime.TryParse(expireTime, out date) ? 
                date.AddMinutes(-10) : 
                DateTime.Now.AddSeconds(Convert.ToDouble(expireTime)).AddMinutes(-10);

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

        public class Tra
        {
            public string CodDte { get; set; }
            public string Url { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
        }

        public List<Token.Tra> CriarListaTra()
        {
            var listaTra = new List<Token.Tra>();

            var traPoc = new Token.Tra()
            {
                CodDte = "007",
                Url = "http://webapi.trapoc.com.br:3333/eGMCI/api/token",
                Username = "TESTpoc",
                Password = "passPoc"
            };

            
            listaTra.Add(traPoc);

            return listaTra;
        }
    }
}
