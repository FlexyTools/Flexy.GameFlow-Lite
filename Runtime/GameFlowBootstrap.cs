using System.Linq;
using System.Reflection;
using UnityEngine.SceneManagement;

namespace Flexy.GameFlow
{
	[DefaultExecutionOrder(Int16.MinValue+100)]
	public class GameFlowBootstrap : MonoBehaviour
	{
		[SerializeField]	protected GameContext			_globalContext;
        [SerializeField]	protected AssetRef<State>[]		_statesToOpen;

		private static GameFlowBootstrap _ref;

		private void Awake()
		{
			// Almost very first Awake in scene thanks to DefaultExecutionOrder
			var isDuplicate = (Boolean)_ref;

			if ( isDuplicate )
			{
				DestroyImmediate( gameObject );
				return;
			}

			_ref = this;
			DontDestroyOnLoad( gameObject );
			
			Debug.Log( $"[GameFlowBootstrap] [Frame:{Time.frameCount}] ----------- ===========   Game GameFlowBootstrap Begin   =========== -----------" );
			Boot();
			Debug.Log( $"[GameFlowBootstrap] [Frame:{Time.frameCount}] ----------- ===========   Game GameFlowBootstrap End   =========== -----------" );
		}

		protected virtual void Boot( )
		{
			_globalContext.gameObject.SetActive( false );
			var gctx	= Instantiate( _globalContext );
			_globalContext.gameObject.SetActive( true );
			
			gctx.name	= _globalContext.name;

			foreach ( var launchService in gameObject.GetComponents<MonoBehaviour>( ) )
			{
				if( launchService == this )
					continue;

				gctx.SetService( launchService );
			}
			
			gctx.gameObject.SetActive(true);

			var openParams		= default(Object);

			#if UNITY_EDITOR
			{
				if (_statesToOpen.Length > 0 && Core.Editor.TestCaseDropdown.TryGetTestCaseToLaunch( "State", out var testCaseName ))
				{
					Debug.Log( "" );
					Debug.Log( "" );
					Debug.Log( $"GameState: TEST LAUNCH    -    {testCaseName}" );
					Debug.Log( "" );
					Debug.Log( "" );

					var stateToOpen = _statesToOpen[^1];

					var wnd		= stateToOpen.LoadAssetSync();
					var m		= wnd.GetType().GetMethod( testCaseName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance );
					openParams	= m?.Invoke( wnd, null );
				}
			}
			#endif

			var service		= gctx.GetService<GameFlowService>( );

            if (_statesToOpen.Length > 1)
	            foreach (var state in _statesToOpen[..^1])
		            service.Graph.Open( state, null );
            
            if (_statesToOpen.Length > 0)
				service.Graph.Open( _statesToOpen[^1], null, openParams );

			// Show first state synchronously
			service.Graph.TransitionNow();
		}
		

#if UNITY_EDITOR
		[UnityEditor.InitializeOnLoad]
		public static class BootstrapTestCasesProvider
		{
			static BootstrapTestCasesProvider( ) => Core.Editor.TestCaseDropdown.AddTestProvider( "State", GetTestRuns );

			private static IEnumerable<String> GetTestRuns( )
			{
				var activeScene		= SceneManager.GetActiveScene( );
				var rootGos			= activeScene.GetRootGameObjects( );
				var bootstraps		= rootGos.Select( go => go.GetComponent<GameFlowBootstrap>() ).Where( c => c is not null );
				var bootstrap		= bootstraps.FirstOrDefault();
				var subStateToOpen	= bootstrap?._statesToOpen[^1] ?? default; 

				if ( !bootstrap || subStateToOpen.IsNone )
					yield break;

				var subState = AssetsLoader.EditorLoadAsset( subStateToOpen );
				if ( !subState )
					yield break;

				var methods = subState.GetType( ).GetMethods( BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic );

				foreach ( var m in methods )
				{
					if( m.GetCustomAttribute<StateTestAttribute>() != null )
						yield return m.Name;
				}
			}
		}
#endif
	}
}