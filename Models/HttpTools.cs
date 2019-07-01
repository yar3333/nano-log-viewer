using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Text;

namespace NanoLogViewer.Models
{
    class HttpTools
    {
        public static int? getSize(string uri)
        {
            var http = createWebRequest(uri);
            http.Method = "HEAD";

            using (var response = http.GetResponse())
            { 
                return response.Headers["Content-Length"] != null ? (int?)int.Parse(response.Headers["Content-Length"]) : null;
            }
        }

        public static string download(string uri, int? offset)
        {
            var http = createWebRequest(uri);
            if (offset != null) http.AddRange(offset.Value);

            using (var response = http.GetResponse())
            { 
                var stream = response.GetResponseStream();
                var sr = new StreamReader(stream);
                return sr.ReadToEnd();
            }
        }

        static HttpWebRequest createWebRequest(string uri)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(delegate { return true; });

            var request = (HttpWebRequest)WebRequest.Create(uri);

            var parsedUri = new Uri(uri);
            if (!string.IsNullOrEmpty(parsedUri.UserInfo))
            {
                string credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(parsedUri.UserInfo));
                request.Headers[HttpRequestHeader.Authorization] = "Basic " + credentials;

            }

            return request;
        }
    }
}
