// A deliberately narrow C shim between Box3D's real API and P/Invoke.
//
// Box3D's own API is not safe to bind directly from C#: b3WorldDef,
// b3BodyDef, and b3ShapeDef are large structs with function pointers and
// nested types passed by value, and b3BoxHull is a struct whose own doc
// comment says "has data hanging off the end and cannot be directly
// copied." Replicating any of that layout byte-for-byte in C# is exactly
// the kind of "compiles, passes a smoke test, breaks on someone else's
// machine" risk docs/kernel-contract.md's own risk table warns about for
// native interop specifically. So none of it crosses the P/Invoke
// boundary: every exported function here takes and returns only plain
// int32_t/float scalars, which marshal automatically with no `unsafe` on
// the C# side at all.
//
// Handles are plain array indices into fixed-capacity tables, not Box3D's
// own id structs — one less thing to get the marshaling of exactly right,
// and plenty of headroom for an indie-scale game (Box3D itself caps out at
// 128 worlds; nothing here needs more than one at a time yet).

#include <stdbool.h>
#include <stdint.h>
#include <box3d/box3d.h>

#if defined( _WIN32 )
#define LINGUA_API __declspec( dllexport )
#else
#define LINGUA_API __attribute__( ( visibility( "default" ) ) )
#endif

#define LINGUA_MAX_WORLDS 8
#define LINGUA_MAX_BODIES 8192

static b3WorldId g_worlds[LINGUA_MAX_WORLDS];
static bool g_worldUsed[LINGUA_MAX_WORLDS];

static b3BodyId g_bodies[LINGUA_MAX_BODIES];
static bool g_bodyUsed[LINGUA_MAX_BODIES];

static bool ValidWorld( int32_t handle )
{
	return handle >= 0 && handle < LINGUA_MAX_WORLDS && g_worldUsed[handle];
}

static bool ValidBody( int32_t handle )
{
	return handle >= 0 && handle < LINGUA_MAX_BODIES && g_bodyUsed[handle];
}

static int32_t AllocBodySlot( void )
{
	for ( int32_t i = 0; i < LINGUA_MAX_BODIES; i++ )
	{
		if ( !g_bodyUsed[i] )
			return i;
	}
	return -1;
}

LINGUA_API int32_t Lingua_CreateWorld( float gravityX, float gravityY, float gravityZ )
{
	for ( int32_t i = 0; i < LINGUA_MAX_WORLDS; i++ )
	{
		if ( g_worldUsed[i] )
			continue;

		b3WorldDef def = b3DefaultWorldDef();
		def.gravity = ( b3Vec3 ){ gravityX, gravityY, gravityZ };

		g_worlds[i] = b3CreateWorld( &def );
		g_worldUsed[i] = true;
		return i;
	}

	return -1;
}

LINGUA_API void Lingua_DestroyWorld( int32_t worldHandle )
{
	if ( !ValidWorld( worldHandle ) )
		return;

	b3DestroyWorld( g_worlds[worldHandle] );
	g_worldUsed[worldHandle] = false;
}

LINGUA_API void Lingua_WorldStep( int32_t worldHandle, float timeStep, int32_t subStepCount )
{
	if ( !ValidWorld( worldHandle ) )
		return;

	b3World_Step( g_worlds[worldHandle], timeStep, subStepCount );
}

static int32_t CreateBody(
	int32_t worldHandle,
	float px, float py, float pz,
	float qx, float qy, float qz, float qw,
	int32_t bodyType )
{
	if ( !ValidWorld( worldHandle ) )
		return -1;

	int32_t slot = AllocBodySlot();
	if ( slot < 0 )
		return -1;

	b3BodyDef bodyDef = b3DefaultBodyDef();
	bodyDef.type = (b3BodyType)bodyType;
	bodyDef.position = ( b3Vec3 ){ px, py, pz };
	bodyDef.rotation = ( b3Quat ){ { qx, qy, qz }, qw };

	g_bodies[slot] = b3CreateBody( g_worlds[worldHandle], &bodyDef );
	g_bodyUsed[slot] = true;
	return slot;
}

static void ShapeDef( float density, float friction, float restitution, b3ShapeDef* def )
{
	*def = b3DefaultShapeDef();
	def->density = density;
	def->baseMaterial.friction = friction;
	def->baseMaterial.restitution = restitution;
}

LINGUA_API int32_t Lingua_CreateBoxBody(
	int32_t worldHandle,
	float px, float py, float pz,
	float qx, float qy, float qz, float qw,
	float halfWidth, float halfHeight, float halfDepth,
	int32_t bodyType, float density, float friction, float restitution )
{
	int32_t handle = CreateBody( worldHandle, px, py, pz, qx, qy, qz, qw, bodyType );
	if ( handle < 0 )
		return -1;

	b3ShapeDef shapeDef;
	ShapeDef( density, friction, restitution, &shapeDef );

	b3BoxHull hull = b3MakeBoxHull( halfWidth, halfHeight, halfDepth );
	b3CreateHullShape( g_bodies[handle], &shapeDef, &hull.base );

	return handle;
}

LINGUA_API int32_t Lingua_CreateSphereBody(
	int32_t worldHandle,
	float px, float py, float pz,
	float qx, float qy, float qz, float qw,
	float radius,
	int32_t bodyType, float density, float friction, float restitution )
{
	int32_t handle = CreateBody( worldHandle, px, py, pz, qx, qy, qz, qw, bodyType );
	if ( handle < 0 )
		return -1;

	b3ShapeDef shapeDef;
	ShapeDef( density, friction, restitution, &shapeDef );

	b3Sphere sphere = { ( b3Vec3 ){ 0, 0, 0 }, radius };
	b3CreateSphereShape( g_bodies[handle], &shapeDef, &sphere );

	return handle;
}

LINGUA_API void Lingua_DestroyBody( int32_t bodyHandle )
{
	if ( !ValidBody( bodyHandle ) )
		return;

	b3DestroyBody( g_bodies[bodyHandle] );
	g_bodyUsed[bodyHandle] = false;
}

LINGUA_API void Lingua_GetBodyTransform(
	int32_t bodyHandle,
	float* outPx, float* outPy, float* outPz,
	float* outQx, float* outQy, float* outQz, float* outQw )
{
	if ( !ValidBody( bodyHandle ) )
		return;

	b3WorldTransform t = b3Body_GetTransform( g_bodies[bodyHandle] );
	*outPx = t.p.x;
	*outPy = t.p.y;
	*outPz = t.p.z;
	*outQx = t.q.v.x;
	*outQy = t.q.v.y;
	*outQz = t.q.v.z;
	*outQw = t.q.s;
}

LINGUA_API void Lingua_SetBodyTransform(
	int32_t bodyHandle,
	float px, float py, float pz,
	float qx, float qy, float qz, float qw )
{
	if ( !ValidBody( bodyHandle ) )
		return;

	b3Body_SetTransform( g_bodies[bodyHandle], ( b3Pos ){ px, py, pz }, ( b3Quat ){ { qx, qy, qz }, qw } );
}

LINGUA_API void Lingua_ApplyLinearImpulse( int32_t bodyHandle, float ix, float iy, float iz, bool wake )
{
	if ( !ValidBody( bodyHandle ) )
		return;

	b3Body_ApplyLinearImpulseToCenter( g_bodies[bodyHandle], ( b3Vec3 ){ ix, iy, iz }, wake );
}

LINGUA_API void Lingua_GetLinearVelocity( int32_t bodyHandle, float* outVx, float* outVy, float* outVz )
{
	if ( !ValidBody( bodyHandle ) )
		return;

	b3Vec3 v = b3Body_GetLinearVelocity( g_bodies[bodyHandle] );
	*outVx = v.x;
	*outVy = v.y;
	*outVz = v.z;
}

LINGUA_API void Lingua_SetLinearVelocity( int32_t bodyHandle, float vx, float vy, float vz )
{
	if ( !ValidBody( bodyHandle ) )
		return;

	b3Body_SetLinearVelocity( g_bodies[bodyHandle], ( b3Vec3 ){ vx, vy, vz } );
}
