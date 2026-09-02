// The same narrow-shim approach as native/physics-native/lingua_physics.c,
// for the same reason: miniaudio's own ma_engine_config/ma_sound_config are
// large structs with optional callbacks, not something worth replicating
// byte-for-byte in C#. Every exported function here takes and returns only
// plain int/float/bool scalars (a C string for the one thing that has to
// be one, a file path), and handles are this shim's own array-index
// handles into a fixed-capacity ma_sound table — not miniaudio's own
// pointer types.
//
// One global ma_engine: nothing in M4's scope needs more than one audio
// device active at a time.

#include <stdbool.h>

#define MINIAUDIO_IMPLEMENTATION
#include "miniaudio.h"

#if defined( _WIN32 )
#define LINGUA_API __declspec( dllexport )
#else
#define LINGUA_API __attribute__( ( visibility( "default" ) ) )
#endif

#define LINGUA_MAX_SOUNDS 64

static ma_engine g_engine;
static int g_engineInitialized = 0;

static ma_sound g_sounds[LINGUA_MAX_SOUNDS];
static int g_soundUsed[LINGUA_MAX_SOUNDS];

static int ValidSound( int handle )
{
	return handle >= 0 && handle < LINGUA_MAX_SOUNDS && g_soundUsed[handle];
}

// useNullBackend: real playback needs a real device, which automated
// tests shouldn't depend on (nondeterministic across machines, and CI
// almost never has one) or want (nobody expects a test run to make sound).
// The null backend runs the exact same mixing/decoding/looping pipeline
// against a device that discards its output — real coverage of this
// shim's logic, silently.
LINGUA_API int Lingua_Audio_Init( int useNullBackend )
{
	if ( g_engineInitialized )
		return 1;

	ma_result result;

	if ( useNullBackend )
	{
		static ma_context context;
		ma_backend backends[] = { ma_backend_null };

		result = ma_context_init( backends, 1, NULL, &context );
		if ( result != MA_SUCCESS )
			return 0;

		ma_engine_config engineConfig = ma_engine_config_init();
		engineConfig.pContext = &context;
		result = ma_engine_init( &engineConfig, &g_engine );
	}
	else
	{
		result = ma_engine_init( NULL, &g_engine );
	}

	if ( result != MA_SUCCESS )
		return 0;

	g_engineInitialized = 1;
	return 1;
}

LINGUA_API void Lingua_Audio_Shutdown( void )
{
	if ( !g_engineInitialized )
		return;

	for ( int i = 0; i < LINGUA_MAX_SOUNDS; i++ )
	{
		if ( g_soundUsed[i] )
		{
			ma_sound_uninit( &g_sounds[i] );
			g_soundUsed[i] = 0;
		}
	}

	ma_engine_uninit( &g_engine );
	g_engineInitialized = 0;
}

LINGUA_API int Lingua_Audio_LoadSound( const char* path )
{
	if ( !g_engineInitialized )
		return -1;

	int slot = -1;
	for ( int i = 0; i < LINGUA_MAX_SOUNDS; i++ )
	{
		if ( !g_soundUsed[i] )
		{
			slot = i;
			break;
		}
	}
	if ( slot < 0 )
		return -1;

	if ( ma_sound_init_from_file( &g_engine, path, 0, NULL, NULL, &g_sounds[slot] ) != MA_SUCCESS )
		return -1;

	g_soundUsed[slot] = 1;
	return slot;
}

LINGUA_API void Lingua_Audio_UnloadSound( int handle )
{
	if ( !ValidSound( handle ) )
		return;

	ma_sound_uninit( &g_sounds[handle] );
	g_soundUsed[handle] = 0;
}

// Always restarts from the first frame — the predictable behavior for
// "trigger this sound effect now." A looping music track only needs this
// called once; calling it again mid-loop is a deliberate restart, not a
// bug.
LINGUA_API void Lingua_Audio_Play( int handle )
{
	if ( !ValidSound( handle ) )
		return;

	ma_sound_seek_to_pcm_frame( &g_sounds[handle], 0 );
	ma_sound_start( &g_sounds[handle] );
}

LINGUA_API void Lingua_Audio_Stop( int handle )
{
	if ( !ValidSound( handle ) )
		return;

	ma_sound_stop( &g_sounds[handle] );
}

LINGUA_API void Lingua_Audio_SetVolume( int handle, float volume )
{
	if ( !ValidSound( handle ) )
		return;

	ma_sound_set_volume( &g_sounds[handle], volume );
}

LINGUA_API void Lingua_Audio_SetLooping( int handle, bool loop )
{
	if ( !ValidSound( handle ) )
		return;

	ma_sound_set_looping( &g_sounds[handle], loop ? MA_TRUE : MA_FALSE );
}

LINGUA_API bool Lingua_Audio_IsPlaying( int handle )
{
	if ( !ValidSound( handle ) )
		return false;

	return ma_sound_is_playing( &g_sounds[handle] ) ? true : false;
}
