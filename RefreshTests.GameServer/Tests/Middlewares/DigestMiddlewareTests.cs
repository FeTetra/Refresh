using System.Text;
using Refresh.GameServer.Middlewares;

namespace RefreshTests.GameServer.Tests.Middlewares;

public class DigestMiddlewareTests : GameServerTest
{
    [Test]
    public void DoesntIncludeDigestWhenOutsideOfGame()
    {
        using TestContext context = this.GetServer();
        context.Server.Value.Server.AddMiddleware(new DigestMiddleware(context.Server.Value.GameServerConfig));

        HttpResponseMessage response =  context.Http.GetAsync("/api/v3/instance").Result;
        
        Assert.Multiple(() =>
        {
            Assert.That(response.Headers.Contains("X-Digest-A"), Is.False);
            Assert.That(response.Headers.Contains("X-Digest-B"), Is.False);
        });
    }
    
    [Test]
    public void IncludesDigestInGame()
    {
        using TestContext context = this.GetServer();
        context.Server.Value.Server.AddEndpointGroup<TestEndpoints>();
        context.Server.Value.Server.AddMiddleware(new DigestMiddleware(context.Server.Value.GameServerConfig));

        HttpResponseMessage response =  context.Http.GetAsync("/lbp/eula").Result;
        
        Assert.Multiple(() =>
        {
            Assert.That(response.Headers.Contains("X-Digest-A"), Is.True);
            Assert.That(response.Headers.Contains("X-Digest-B"), Is.True);
        });
    }
    
    [TestCase(false)]
    [TestCase(true)]
    public void DigestIsCorrect(bool isHmac)
    {
        using TestContext context = this.GetServer();
        context.Server.Value.Server.AddEndpointGroup<TestEndpoints>();
        context.Server.Value.Server.AddMiddleware(new DigestMiddleware(context.Server.Value.GameServerConfig));

        const string endpoint = "/lbp/test";
        const string expectedResultStr = "test";

        string digest = isHmac
            ? context.Server.Value.GameServerConfig.HmacDigestKeys[0]
            : context.Server.Value.GameServerConfig.Sha1DigestKeys[0];
        
        string serverDigest = DigestMiddleware.CalculateDigest(digest, endpoint, Encoding.ASCII.GetBytes(expectedResultStr), "", null, false, isHmac);
        string clientDigest = DigestMiddleware.CalculateDigest(digest, endpoint, [], "", null, false, isHmac);

        context.Http.DefaultRequestHeaders.Add("X-Digest-A", clientDigest);
        HttpResponseMessage response =  context.Http.GetAsync(endpoint).Result;
        
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(OK));
            
            Assert.That(response.Headers.Contains("X-Digest-A"), Is.True);
            Assert.That(response.Headers.Contains("X-Digest-B"), Is.True);
            
            Assert.That(response.Headers.GetValues("X-Digest-A").First(), Is.EqualTo(serverDigest));
            Assert.That(response.Headers.GetValues("X-Digest-B").First(), Is.EqualTo(clientDigest));
        });
    }
    
    [TestCase(false)]
    [TestCase(true)]
    public void CanFindSecondaryDigest(bool isHmac)
    {
        using TestContext context = this.GetServer();
        context.Server.Value.Server.AddEndpointGroup<TestEndpoints>();
        context.Server.Value.Server.AddMiddleware(new DigestMiddleware(context.Server.Value.GameServerConfig));
        context.Server.Value.GameServerConfig.Sha1DigestKeys = ["sha1_digest1", "sha1_digest2"];
        context.Server.Value.GameServerConfig.HmacDigestKeys = ["hmac_digest1", "hmac_digest2"];

        const string endpoint = "/lbp/test";
        const string expectedResultStr = "test";

        string digest = isHmac
            ? context.Server.Value.GameServerConfig.HmacDigestKeys[1]
            : context.Server.Value.GameServerConfig.Sha1DigestKeys[1];
        
        string serverDigest = DigestMiddleware.CalculateDigest(digest, endpoint, Encoding.ASCII.GetBytes(expectedResultStr), "", null, false, isHmac);
        string clientDigest = DigestMiddleware.CalculateDigest(digest, endpoint, [], "", null, false, isHmac);

        context.Http.DefaultRequestHeaders.Add("X-Digest-A", clientDigest);
        HttpResponseMessage response =  context.Http.GetAsync(endpoint).Result;
        
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(OK));
            
            Assert.That(response.Headers.Contains("X-Digest-A"), Is.True);
            Assert.That(response.Headers.Contains("X-Digest-B"), Is.True);
            
            Assert.That(response.Headers.GetValues("X-Digest-A").First(), Is.EqualTo(serverDigest));
            Assert.That(response.Headers.GetValues("X-Digest-B").First(), Is.EqualTo(clientDigest));
        });
    }
    
    [TestCase(false)]
    [TestCase(true)]
    public void DigestSelectionHeaderWorks(bool isHmac)
    {
        using TestContext context = this.GetServer();
        context.Server.Value.Server.AddEndpointGroup<TestEndpoints>();
        context.Server.Value.Server.AddMiddleware(new DigestMiddleware(context.Server.Value.GameServerConfig));
        context.Server.Value.GameServerConfig.Sha1DigestKeys = ["sha1_digest1", "sha1_digest2"];
        context.Server.Value.GameServerConfig.HmacDigestKeys = ["hmac_digest1", "hmac_digest2"];

        const string endpoint = "/lbp/test";
        const string expectedResultStr = "test";

        string digest = isHmac
            ? context.Server.Value.GameServerConfig.HmacDigestKeys[1]
            : context.Server.Value.GameServerConfig.Sha1DigestKeys[1];
        
        string serverDigest = DigestMiddleware.CalculateDigest(digest, endpoint, Encoding.ASCII.GetBytes(expectedResultStr), "", null, false, isHmac);
        
        // TODO: once we model PS4 clients in our tokens, make the request come from an authenticated PS4 client.
        if(isHmac)
            context.Http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "MM CHTTPClient LBP3 01.26");

        // send a blank digest to make it have to guess
        context.Http.DefaultRequestHeaders.Add("X-Digest-A", "");
        HttpResponseMessage response =  context.Http.GetAsync(endpoint + "?force_ps3_digest=1&force_ps4_digest=1").Result;
        
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(OK));
            
            Assert.That(response.Headers.Contains("X-Digest-A"), Is.True);
            Assert.That(response.Headers.Contains("X-Digest-B"), Is.True);

            // Ensure that the server digest used the second digest key, even though without the headers it would default to the first
            Assert.That(response.Headers.GetValues("X-Digest-A").First(), Is.EqualTo(serverDigest));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FallsBackToFirstDigest(bool isHmac)
    {
        using TestContext context = this.GetServer();
        context.Server.Value.Server.AddEndpointGroup<TestEndpoints>();
        context.Server.Value.Server.AddMiddleware(new DigestMiddleware(context.Server.Value.GameServerConfig));

        const string endpoint = "/lbp/test";
        const string expectedResultStr = "test";

        string digest = isHmac
            ? context.Server.Value.GameServerConfig.HmacDigestKeys[0]
            : context.Server.Value.GameServerConfig.Sha1DigestKeys[0];

        // Calculate the digest response as if the digest used was the first digest
        string serverDigest = DigestMiddleware.CalculateDigest(digest, endpoint,
            Encoding.ASCII.GetBytes(expectedResultStr), "", null, false, isHmac);

        // TODO: once we model PS4 clients in our tokens, make the request come from an authenticated PS4 client.
        if(isHmac)
            context.Http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "MM CHTTPClient LBP3 01.26");
        
        context.Http.DefaultRequestHeaders.Add("X-Digest-A", "nonsense digest");
        HttpResponseMessage response = context.Http.GetAsync(endpoint).Result;

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(OK));

            Assert.That(response.Headers.Contains("X-Digest-A"), Is.True);
            Assert.That(response.Headers.Contains("X-Digest-B"), Is.True);

            Assert.That(response.Headers.GetValues("X-Digest-A").First(), Is.EqualTo(serverDigest));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PspDigestIsCorrect(bool isHmac)
    {
        using TestContext context = this.GetServer();
        context.Server.Value.Server.AddEndpointGroup<TestEndpoints>();
        context.Server.Value.Server.AddMiddleware(new DigestMiddleware(context.Server.Value.GameServerConfig));

        const string endpoint = "/lbp/test";
        const string expectedResultStr = "test";
        
        string digest = isHmac
            ? context.Server.Value.GameServerConfig.HmacDigestKeys[0]
            : context.Server.Value.GameServerConfig.Sha1DigestKeys[0];
        
        string serverDigest = DigestMiddleware.CalculateDigest(digest, endpoint, Encoding.ASCII.GetBytes(expectedResultStr), "", null, false, isHmac);
        string clientDigest = DigestMiddleware.CalculateDigest(digest, endpoint, [], "", new DigestMiddleware.PspVersionInfo(205, 5), false, isHmac);

        context.Http.DefaultRequestHeaders.Add("X-Digest-A", clientDigest);
        context.Http.DefaultRequestHeaders.Add("X-data-v", "5");
        context.Http.DefaultRequestHeaders.Add("X-exe-v", "205");
        HttpResponseMessage response =  context.Http.GetAsync(endpoint).Result;
        
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(OK));
            
            Assert.That(response.Headers.Contains("X-Digest-A"), Is.True);
            Assert.That(response.Headers.Contains("X-Digest-B"), Is.True);
            
            Assert.That(response.Headers.GetValues("X-Digest-A").First(), Is.EqualTo(serverDigest));
            Assert.That(response.Headers.GetValues("X-Digest-B").First(), Is.EqualTo(clientDigest));
        });
    }
}