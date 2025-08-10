using System.Linq;
using System.Reflection;

namespace Flexy.GameFlow
{
	[DefaultExecutionOrder(Int16.MinValue+100)]
	public class GameFlowBootstrap : MonoBehaviour
	{
		[SerializeField]	protected GameContext			_globalContext;
        [FormerlySerializedAs("_statesToOpen")] 
        [SerializeField]	protected AssetRef<State>[]		_bootstrapContext;
        [SerializeField]	protected AssetRef<State>		_bootstrapTarget;

		private static GameFlowBootstrap _ref;

		private void Awake( )
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
			
			Debug.Log( $"[GameFlowBootstrap] [Frame:{Time.frameCount}] ----------- ===========   GameFlow Bootstrap Begin   =========== -----------" );
			Boot();
			Debug.Log( $"[GameFlowBootstrap] [Frame:{Time.frameCount}] ----------- ===========   GameFlow Bootstrap End   =========== -----------" );
		}

		protected virtual void Boot( )
		{
			_globalContext.gameObject.SetActive( false );
			var gctx	= Instantiate( _globalContext );
			_globalContext.gameObject.SetActive( true );
			_globalContext.gameObject.ClearEditorDirty();
			
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
				if (_bootstrapContext.Length > 0 && Core.Editor.TestCaseDropdown.TryGetTestCaseToLaunch( "State", out var testCaseName ))
				{
					Debug.Log( "" );
					Debug.Log( "" );
					Debug.Log( $"[GameFlowBootstrap] TEST LAUNCH    -    {testCaseName}" );
					Debug.Log( "" );
					Debug.Log( "" );

					var state	= _bootstrapTarget.LoadAssetSync();
					var m		= state.GetType().GetMethod( testCaseName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance );
					openParams	= m?.Invoke( state, null );
				}
			}
			#endif

			var service		= gctx.GetService<Service_GameFlow>( );

            foreach (var state in _bootstrapContext)
	            service.Graph.Open( state, null );
            
            if (!_bootstrapTarget.IsNone)
				service.Graph.Open( _bootstrapTarget, null, openParams );

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
				var subStateToOpen	= bootstrap?._bootstrapTarget ?? default; 

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