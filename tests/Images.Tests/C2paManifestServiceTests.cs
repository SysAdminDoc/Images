using Images.Services;

namespace Images.Tests;

public sealed class C2paManifestServiceTests
{
    [Fact]
    public void ParseManifestJson_ReadsActiveManifestAndClaims()
    {
        var json = """
        {
          "active_manifest": "urn:uuid:abc123",
          "manifests": {
            "urn:uuid:abc123": {
              "title": "photo.jpg",
              "format": "image/jpeg",
              "instance_id": "xmp:iid:12345",
              "claim_generator": "Adobe Photoshop 26.0",
              "signature_info": {
                "issuer": "Adobe Inc.",
                "time": "2026-01-15T10:30:00Z"
              },
              "assertions": [
                {
                  "label": "c2pa.actions",
                  "data": {
                    "actions": [
                      { "action": "c2pa.created" },
                      { "action": "c2pa.edited" }
                    ]
                  }
                },
                {
                  "label": "c2pa.hash.data",
                  "data": { "name": "sha256" }
                }
              ],
              "ingredients": [
                {
                  "title": "original.jpg",
                  "format": "image/jpeg",
                  "relationship": "parentOf"
                }
              ]
            }
          }
        }
        """;

        var result = C2paManifestService.ParseManifestJson(json);

        Assert.Equal(C2paStatus.Found, result.Status);
        Assert.True(result.HasCredentials);
        Assert.Equal("urn:uuid:abc123", result.ActiveManifestLabel);
        Assert.Single(result.Claims);

        var claim = result.Claims[0];
        Assert.Equal("urn:uuid:abc123", claim.Label);
        Assert.Equal("photo.jpg", claim.Title);
        Assert.Equal("image/jpeg", claim.Format);
        Assert.Equal("Adobe Photoshop 26.0", claim.ClaimGenerator);
        Assert.Equal("2026-01-15T10:30:00Z", claim.SignatureDate);
        Assert.Equal("Adobe Inc.", claim.SignatureIssuer);
        Assert.Equal(2, claim.Assertions.Count);
        Assert.Contains("c2pa.created", claim.Assertions[0].Summary);
        Assert.Single(claim.Ingredients);
        Assert.Equal("original.jpg", claim.Ingredients[0].Title);
    }

    [Fact]
    public void ParseManifestJson_EmptyManifests_ReturnsNoManifest()
    {
        var json = """{ "active_manifest": null, "manifests": {} }""";

        var result = C2paManifestService.ParseManifestJson(json);

        Assert.Equal(C2paStatus.NoManifest, result.Status);
        Assert.False(result.HasCredentials);
    }

    [Fact]
    public void ParseManifestJson_InvalidJson_ReturnsError()
    {
        var result = C2paManifestService.ParseManifestJson("not json at all");

        Assert.Equal(C2paStatus.Error, result.Status);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void ParseManifestJson_MultipleManifests_PicksActiveByLabel()
    {
        var json = """
        {
          "active_manifest": "urn:uuid:second",
          "manifests": {
            "urn:uuid:first": {
              "claim_generator": "Camera Firmware",
              "signature_info": { "issuer": "Sony" }
            },
            "urn:uuid:second": {
              "claim_generator": "Lightroom Classic 14.0",
              "signature_info": { "issuer": "Adobe Inc." }
            }
          }
        }
        """;

        var result = C2paManifestService.ParseManifestJson(json);

        Assert.Equal(C2paStatus.Found, result.Status);
        Assert.Equal(2, result.Claims.Count);
        Assert.Equal("urn:uuid:second", result.ActiveManifestLabel);
    }

    [Fact]
    public void ParseManifestJson_ValidationFailures_SetInvalidTrustLevel()
    {
        var json = """
        {
          "active_manifest": "urn:uuid:test",
          "manifests": {
            "urn:uuid:test": {
              "claim_generator": "test",
              "signature_info": { "issuer": "test" }
            }
          },
          "validation_status": [
            { "code": "assertion.hashedURI.failure" }
          ]
        }
        """;

        var result = C2paManifestService.ParseManifestJson(json);

        Assert.Equal(C2paStatus.Found, result.Status);
        Assert.Equal(C2paTrustLevel.Invalid, result.TrustLevel);
    }

    [Fact]
    public void Read_UnsupportedExtension_ReturnsNoManifest()
    {
        using var temp = TestDirectory.Create();
        var path = temp.WriteFile("test.bmp", "not a c2pa-supported file");

        var result = C2paManifestService.Read(
            path,
            executableOverride: null,
            processRunner: null);

        Assert.Equal(C2paStatus.NoManifest, result.Status);
        Assert.Contains("does not support", result.ErrorMessage!, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_MissingFile_ReturnsNoManifest()
    {
        var result = C2paManifestService.Read(
            "/nonexistent/path.jpg",
            executableOverride: null,
            processRunner: null);

        Assert.Equal(C2paStatus.NoManifest, result.Status);
    }
}
