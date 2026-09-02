using System.Runtime.CompilerServices;

// PngReader is internal — nothing outside this plugin needs it, texture
// loading goes through IAssetService. The test project needs direct access
// to test the codec itself without going through a GL-dependent round trip.
[assembly: InternalsVisibleTo("Engine.Assets.Tests")]
