using System.Runtime.CompilerServices;

// PhysicsWorld is internal — nothing outside this plugin needs it, real
// physics behavior is tested against it directly rather than only through
// IPlugin/IPhysicsService's much narrower surface. Same pattern as
// Engine.Assets' PngReader.
[assembly: InternalsVisibleTo("Engine.Physics.Tests")]
