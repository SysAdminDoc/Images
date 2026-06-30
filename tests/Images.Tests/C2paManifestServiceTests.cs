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
    public void PlanExportHandoff_WhenTargetFormatIsUnsupported_OmitsWithoutInspectingSource()
    {
        using var temp = TestDirectory.Create();
        var source = temp.WriteFile("credentialed.jpg", "fake image bytes");
        var inspected = false;

        var result = C2paManifestService.PlanExportHandoff(
            source,
            ".jxl",
            readManifest: _ =>
            {
                inspected = true;
                return FoundCredentials();
            },
            inspectRuntime: () => throw new InvalidOperationException("runtime should not be inspected"),
            inspectWriter: C2paExportWriterRuntimeStatus.NotConfigured);

        Assert.Equal(C2paExportAction.Omit, result.Action);
        Assert.Equal(C2paExportReason.TargetFormatUnsupported, result.Reason);
        Assert.False(inspected);
    }

    [Fact]
    public void PlanExportHandoff_WhenRuntimeUnavailable_OmitsWithoutReadingManifest()
    {
        using var temp = TestDirectory.Create();
        var source = temp.WriteFile("credentialed.jpg", "fake image bytes");
        var inspected = false;

        var result = C2paManifestService.PlanExportHandoff(
            source,
            ".png",
            readManifest: _ =>
            {
                inspected = true;
                return FoundCredentials();
            },
            inspectRuntime: () => C2paToolRuntimeStatus.Missing("c2patool not found"),
            inspectWriter: C2paExportWriterRuntimeStatus.NotConfigured);

        Assert.Equal(C2paExportAction.Omit, result.Action);
        Assert.Equal(C2paExportReason.InspectionRuntimeUnavailable, result.Reason);
        Assert.True(result.RequiresAttention);
        Assert.False(inspected);
    }

    [Fact]
    public void PlanExportHandoff_WhenSourceHasNoManifest_ReportsNoWrite()
    {
        using var temp = TestDirectory.Create();
        var source = temp.WriteFile("plain.jpg", "fake image bytes");

        var result = C2paManifestService.PlanExportHandoff(
            source,
            ".png",
            readManifest: _ => C2paInspectionResult.NoManifest("No manifest"),
            inspectRuntime: ReadyRuntime,
            inspectWriter: C2paExportWriterRuntimeStatus.NotConfigured);

        Assert.Equal(C2paExportAction.Omit, result.Action);
        Assert.Equal(C2paExportReason.SourceHasNoManifest, result.Reason);
        Assert.False(result.RequiresAttention);
        Assert.False(result.HasSourceCredentials);
        Assert.Contains("No source Content Credentials", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanExportHandoff_WhenSourceHasManifestButNoWriter_ReportsOmission()
    {
        using var temp = TestDirectory.Create();
        var source = temp.WriteFile("credentialed.jpg", "fake image bytes");

        var result = C2paManifestService.PlanExportHandoff(
            source,
            ".png",
            readManifest: _ => FoundCredentials(),
            inspectRuntime: ReadyRuntime,
            inspectWriter: C2paExportWriterRuntimeStatus.NotConfigured);

        Assert.Equal(C2paExportAction.Omit, result.Action);
        Assert.Equal(C2paExportReason.WriterNotConfigured, result.Reason);
        Assert.True(result.HasSourceCredentials);
        Assert.True(result.RequiresAttention);
        Assert.Contains("will not copy stale", result.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PlanExportHandoff_WhenApprovedWriterIsAvailable_ReportsWriteHandoff()
    {
        using var temp = TestDirectory.Create();
        var source = temp.WriteFile("credentialed.jpg", "fake image bytes");

        var result = C2paManifestService.PlanExportHandoff(
            source,
            ".png",
            readManifest: _ => FoundCredentials(),
            inspectRuntime: ReadyRuntime,
            inspectWriter: () => C2paExportWriterRuntimeStatus.ApprovedRuntime("test signer", "ready"));

        Assert.Equal(C2paExportAction.WriteWithRuntime, result.Action);
        Assert.Equal(C2paExportReason.ReadyToWrite, result.Reason);
        Assert.True(result.WillWrite);
        Assert.Contains("test signer", result.Detail, StringComparison.Ordinal);
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

    private static C2paToolRuntimeStatus ReadyRuntime() => new(
        Available: true,
        ExecutablePath: @"C:\Tools\c2patool.exe",
        Source: "test",
        Version: "test",
        Sha256: "sha256:test",
        StatusText: "c2patool ready");

    private static C2paInspectionResult FoundCredentials() => new(
        Status: C2paStatus.Found,
        TrustLevel: C2paTrustLevel.Signed,
        ActiveManifestLabel: "urn:uuid:test",
        Claims:
        [
            new C2paClaim(
                Label: "urn:uuid:test",
                Title: "credentialed.jpg",
                Format: "image/jpeg",
                InstanceId: "xmp:iid:test",
                ClaimGenerator: "test",
                SignatureDate: "2026-06-30T00:00:00Z",
                SignatureIssuer: "test issuer",
                Assertions: [],
                Ingredients: [])
        ],
        ErrorMessage: null,
        RawJson: "{}");
}
