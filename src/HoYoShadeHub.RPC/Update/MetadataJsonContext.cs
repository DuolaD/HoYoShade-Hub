using HoYoShadeHub.RPC.Update.Github;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HoYoShadeHub.RPC.Update.Metadata;

[JsonSerializable(typeof(ReleaseInfo))]
[JsonSerializable(typeof(ReleaseManifest))]
[JsonSerializable(typeof(GithubRelease))]
[JsonSerializable(typeof(List<GithubRelease>))]
[JsonSerializable(typeof(GithubMarkdownRequest))]
internal partial class MetadataJsonContext : JsonSerializerContext
{

}
