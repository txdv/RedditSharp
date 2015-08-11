using System;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using System.Threading.Tasks;
using RedditSharp.Things;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RedditSharp
{
    public partial class Reddit
    {
        /// <summary>
        /// Logs in the current Reddit instance.
        /// </summary>
        /// <param name="username">The username of the user to log on to.</param>
        /// <param name="password">The password of the user to log on to.</param>
        /// <param name="useSsl">Whether to use SSL or not. (default: true)</param>
        /// <returns></returns>
        public async Task<AuthenticatedUser> LogInAsync(string username, string password, bool useSsl = true)
        {
            if (Type.GetType("Mono.Runtime") != null)
                ServicePointManager.ServerCertificateValidationCallback = (s, c, ch, ssl) => true;
            _webAgent.Cookies = new CookieContainer();
            HttpWebRequest request;
            if (useSsl)
                request = await _webAgent.CreatePostAsync(SslLoginUrl);
            else
                request = await _webAgent.CreatePostAsync(LoginUrl);
            var stream = request.GetRequestStream();
            if (useSsl)
            {
                await _webAgent.WritePostBodyAsync(stream, new
                    {
                        user = username,
                        passwd = password,
                        api_type = "json"
                    });
            }
            else
            {
                await _webAgent.WritePostBodyAsync(stream, new
                    {
                        user = username,
                        passwd = password,
                        api_type = "json",
                        op = "login"
                    });
            }
            stream.Close();
            var response = (HttpWebResponse)await request.GetResponseAsync();
            var result = await _webAgent.GetResponseStringAsync(response.GetResponseStream());
            var json = JObject.Parse(result)["json"];
            if (json["errors"].Count() != 0)
                throw new AuthenticationException("Incorrect login.");

            await InitOrUpdateUserAsync();

            return User;
        }

        public async Task<RedditUser> GetUserAsync(string name)
        {
            var request = await _webAgent.CreateGetAsync(string.Format(UserInfoUrl, name));
            var response = await request.GetResponseAsync();
            var result = await _webAgent.GetResponseStringAsync(response.GetResponseStream());
            var json = JObject.Parse(result);
            return await new RedditUser().InitAsync(this, json, _webAgent);
        }

        /// <summary>
        /// Initializes the User property if it's null,
        /// otherwise replaces the existing user object
        /// with a new one fetched from reddit servers.
        /// </summary>
        public async Task InitOrUpdateUserAsync()
        {
            var request = await _webAgent.CreateGetAsync(string.IsNullOrEmpty(_webAgent.AccessToken) ? MeUrl : OAuthMeUrl);
            var response = (HttpWebResponse)await request.GetResponseAsync();
            var result = await _webAgent.GetResponseStringAsync(response.GetResponseStream());
            var json = JObject.Parse(result);
            User = await new AuthenticatedUser().InitAsync(this, json, _webAgent);
        }

        /// <summary>
        /// Returns the subreddit. 
        /// </summary>
        /// <param name="name">The name of the subreddit</param>
        /// <returns>The Subreddit by given name</returns>
        public async Task<Subreddit> GetSubredditAsync(string name)
        {
            return await GetThingAsync<Subreddit>(string.Format(SubredditAboutUrl, FixSubreddit(name)));
        }

        public async Task<JToken> GetTokenAsync(Uri uri)
        {
            var url = uri.AbsoluteUri;

            if (url.EndsWith("/"))
                url = url.Remove(url.Length - 1);

            var request = await _webAgent.CreateGetAsync(string.Format(GetPostUrl, url));
            var response = await request.GetResponseAsync();
            var data = await _webAgent.GetResponseStringAsync(response.GetResponseStream());
            var json = JToken.Parse(data);

            return json[0]["data"]["children"].First;
        }

        public async Task<Post> GetPostAsync(Uri uri)
        {
            return await new Post().InitAsync(this, GetToken(uri), _webAgent);
        }

        public async Task ComposePrivateMessageAsync(string subject, string body, string to, string captchaId = "", string captchaAnswer = "")
        {
            if (User == null)
                throw new Exception("User can not be null.");
            var request = await _webAgent.CreatePostAsync(ComposeMessageUrl);
            await _webAgent.WritePostBodyAsync(request.GetRequestStream(), new
                {
                    api_type = "json",
                    subject,
                    text = body,
                    to,
                    uh = User.Modhash,
                    iden = captchaId,
                    captcha = captchaAnswer
                });
            var response = await request.GetResponseAsync();
            var result = await _webAgent.GetResponseStringAsync(response.GetResponseStream());
            var json = JObject.Parse(result);

            ICaptchaSolver solver = CaptchaSolver; // Prevent race condition

            if (json["json"]["errors"].Any() && json["json"]["errors"][0][0].ToString() == "BAD_CAPTCHA" && solver != null)
            {
                captchaId = json["json"]["captcha"].ToString();
                CaptchaResponse captchaResponse = solver.HandleCaptcha(new Captcha(captchaId));

                if (!captchaResponse.Cancel) // Keep trying until we are told to cancel
                    ComposePrivateMessage(subject, body, to, captchaId, captchaResponse.Answer);
            }
        }

        /// <summary>
        /// Registers a new Reddit user
        /// </summary>
        /// <param name="userName">The username for the new account.</param>
        /// <param name="passwd">The password for the new account.</param>
        /// <param name="email">The optional recovery email for the new account.</param>
        /// <returns>The newly created user account</returns>
        public async Task<AuthenticatedUser> RegisterAccountAsync(string userName, string passwd, string email = "")
        {
            var request = await _webAgent.CreatePostAsync(RegisterAccountUrl);
            await _webAgent.WritePostBodyAsync(request.GetRequestStream(), new
                {
                    api_type = "json",
                    email = email,
                    passwd = passwd,
                    passwd2 = passwd,
                    user = userName
                });
            var response = await request.GetResponseAsync();
            var result = await _webAgent.GetResponseStringAsync(response.GetResponseStream());
            var json = JObject.Parse(result);
            return await new AuthenticatedUser().InitAsync(this, json, _webAgent);
            // TODO: Error
        }

        public async Task<Thing> GetThingByFullnameAsync(string fullname)
        {
            var request = await _webAgent.CreateGetAsync(string.Format(GetThingUrl, fullname));
            var response = await request.GetResponseAsync();
            var data = await _webAgent.GetResponseStringAsync(response.GetResponseStream());
            var json = JToken.Parse(data);
            return Thing.Parse(this, json["data"]["children"][0], _webAgent);
        }

        public async Task<Comment> GetCommentAsync(string subreddit, string name, string linkName)
        {
            try
            {
                return await GetCommentAsync(CreateUri(subreddit, linkName, name));
            }
            catch (WebException)
            {
                return null;
            }
        }

        public async Task<Comment> GetCommentAsync(Uri uri)
        {
            var url = string.Format(GetPostUrl, uri.AbsoluteUri);
            var request = await _webAgent.CreateGetAsync(url);
            var response = await request.GetResponseAsync();
            var data = await _webAgent.GetResponseStringAsync(response.GetResponseStream());
            var json = JToken.Parse(data);

            var sender = await new Post().InitAsync(this, json[0]["data"]["children"][0], _webAgent);
            return await new Comment().InitAsync(this, json[1]["data"]["children"][0], _webAgent, sender);
        }

        // Search

        protected async internal Task<T> GetThingAsync<T>(string url) where T : Thing
        {
            var request = await _webAgent.CreateGetAsync(url);
            var response = await request.GetResponseAsync();
            var data = await _webAgent.GetResponseStringAsync(response.GetResponseStream());
            var json = JToken.Parse(data);
            var ret = await Thing.ParseAsync(this, json, _webAgent);
            return (T)ret;
        }
    }
}

