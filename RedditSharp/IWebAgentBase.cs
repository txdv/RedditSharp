using System;
using System.Net;

namespace RedditSharp
{
    public interface IWebAgentBase
    {
        CookieContainer Cookies { get; set; }
        string AuthCookie { get; set; }
        string AccessToken { get; set; }
    }
}

