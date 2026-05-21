using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using NotificationService.Application.Abstractions;
using NotificationService.Domain.Enums;

namespace NotificationService.Infrastructure.Providers.LegacyLink
{
    public sealed class LegacyLinkProvider(IHttpClientFactory httpClientFactory) : IMessageProvider
    {
        private readonly HttpClient _client = httpClientFactory.CreateClient("LegacyLink");

        public IReadOnlySet<MessageFormat> SupportedFormats { get; } =
            new HashSet<MessageFormat>
            {
            MessageFormat.Sms
            };

        public async Task<string> SendAsync(
            MessageFormat format,
            string message,
            string recipient,
            IReadOnlyDictionary<string, string> credentials,
            CancellationToken ct)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
            ArgumentException.ThrowIfNullOrWhiteSpace(recipient);

            if (!credentials.TryGetValue("Username", out var username))
            {
                throw new ArgumentException("Missing credential: Username");
            }

            if (!credentials.TryGetValue("Password", out var password))
            {
                throw new ArgumentException("Missing credential: Password");
            }

            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));

            XNamespace ns = "http://legacylink.fakecomworld.com/v1";
            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(ns + "SendSmsRequest",
                    new XElement(ns + "PhoneNumber", recipient),
                    new XElement(ns + "MessageText", message),
                    new XElement(ns + "SenderIdentification", "NotificationService")));

            using var request = new HttpRequestMessage(HttpMethod.Post, "SendSms");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
            request.Content = new StringContent(doc.ToString(), Encoding.UTF8, "application/xml");

            using var response = await _client.SendAsync(request, ct);
            var responseXml = await response.Content.ReadAsStringAsync(ct);
            response.EnsureSuccessStatusCode();

            var document = XDocument.Parse(responseXml);

            var messageReference = document
                .Descendants(ns + "MessageReference")
                .FirstOrDefault()?
                .Value;

            if (string.IsNullOrWhiteSpace(messageReference))
            {
                throw new InvalidOperationException("LegacyLink response did not contain a MessageReference.");
            }

            return messageReference;
        }
    }
}