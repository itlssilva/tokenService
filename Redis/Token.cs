using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace Redis
{
    public class Token
    {
        public Token()
        {
            string hostIdentityServer = "http://gmci.bandeiranteslog.com.br:3010/api/oauth/token";
            string usuario = "btp";
            string senha = "BR@51lT3rMiN4L";
            var token = ObterTokenPorUsuario(hostIdentityServer, usuario, senha);
        }

        private string ObterTokenPorUsuario(string hostIdentityServer, string usuario, string senha)
        {
            try
            {
                var credentials = String.Format("{0}:{1}", usuario, senha);
                using (var client = new HttpClient())
                {
                    ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                    //Define Headers
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", "YnRwOkJSQDUxbFQzck1pTjRMWW5KaGMybHNkR1Z5YldsdVlXdz0=");

                    //Prepare Request Body
                    List<KeyValuePair<string, string>> requestData = new List<KeyValuePair<string, string>>();

                    requestData.Add(new KeyValuePair<string, string>("username", usuario));
                    requestData.Add(new KeyValuePair<string, string>("password", senha));
                    requestData.Add(new KeyValuePair<string, string>("grant_type", "password"));

                    FormUrlEncodedContent requestBody = new FormUrlEncodedContent(requestData);

                    //Request Token
                    var request = client.PostAsync(hostIdentityServer, requestBody).Result;
                    var token = request.Content.ReadAsStringAsync();
                    var accessToken = JsonConvert.DeserializeObject<AccessToken>(token.Result);
                    return accessToken.access_token;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Falha ao obter token.\nDetalhe: " + ex.GetBaseException().Message);
            }
        }

        public class AccessToken
        {
            [JsonProperty(PropertyName = "AccessToken")]
            public string access_token { get; set; }
            public string expires_in { get; set; }
            public string token_type { get; set; }
        }
    }
}
