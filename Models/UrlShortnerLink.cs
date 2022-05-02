using System;
using Microsoft.AspNetCore.WebUtilities;

namespace UrlShortnerService.Models
{
    public class UrlShortnerLink
    {
        public int Id { get; set; }
        public string Url { get; set; }

        public string GetShortUrl()
        {
            // Transform the "Id" property on this object into a short piece of text
            return WebEncoders.Base64UrlEncode(BitConverter.GetBytes(Id));
        }

        public static int GetId(string url)
        {
            // Reverse our short url text back into an interger Id
            return BitConverter.ToInt32(WebEncoders.Base64UrlDecode(url));
        }
    }
}
