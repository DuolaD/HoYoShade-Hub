using HoYoShadeHub.Setup.Core.Github;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HoYoShadeHub.Setup.Core;

[JsonSerializable(typeof(ReleaseInfo))]
[JsonSerializable(typeof(ReleaseManifest))]
[JsonSerializable(typeof(GithubRelease))]
[JsonSerializable(typeof(List<GithubRelease>))]
[JsonSerializable(typeof(GithubMarkdownRequest))]
internal partial class ReleaseJsonContext : JsonSerializerContext { }
