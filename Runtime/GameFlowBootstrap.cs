using System.Linq;
using System.Reflection;

namespace Flexy.GameFlow
{
	[DefaultExecutionOrder(Int16.MinValue+100)]
	public class GameFlowBootstrap : MonoBehaviour
	{
		[SerializeField]	protected Service_GameFlow		_flowService = null!;
        [SerializeField]	protected AssetRef<State>[]		_bootstrapContext = null!;
        [SerializeField]	protected AssetRef<State>		_bootstrapTarget;

		private static GameFlowBootstrap? _ref;

		private				void	Awake	( )		
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
		protected virtual	void	Boot	( )		
		{
			_flowService.gameObject.SetActive( false );
			var gameFlow	= Instantiate( _flowService );
			_flowService.gameObject.SetActive( true );
			_flowService.gameObject.ClearEditorDirty();
			
			gameFlow.name	= _flowService.name;
			var gctx		= gameFlow.GetComponent<GameContext>();
			
			foreach ( var launchService in gameObject.GetComponents<MonoBehaviour>( ) )
			{
				if( launchService == this )
					continue;

				gctx.SetService( launchService );
			}
			
			gameFlow.gameObject.SetActive(true);

			var openParams	= default(Object);

			#if UNITY_EDITOR
			{
				if (_bootstrapContext.Length > 0 && Core.Editor.ToolbarControls.TestCaseDropdown.TryGetTestCaseToLaunch( "State", out var testCaseName ))
				{
					Debug.Log( "" );
					Debug.Log( "" );
					Debug.Log( $"[GameFlowBootstrap] TEST LAUNCH    -    {testCaseName}" );
					Debug.Log( "" );
					Debug.Log( "" );

					var state	= _bootstrapTarget.LoadAssetSync();
					var m		= state?.GetType().GetMethod( testCaseName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance );
					openParams	= m?.Invoke( state, null );
				}
			}
			#endif

            foreach (var state in _bootstrapContext)
	            gameFlow.Graph.Open( state, null );
            
            if (!_bootstrapTarget.IsNone)
	            gameFlow.Graph.Open( _bootstrapTarget, null, openParams );

			// Show first state synchronously
			gameFlow.Graph.TransitionNow();
		}

#if UNITY_EDITOR
		[UnityEditor.InitializeOnLoad]
		public static class BootstrapTestCasesProvider
		{
			static BootstrapTestCasesProvider( ) => Core.Editor.ToolbarControls.TestCaseDropdown.AddTestProvider( "State", GetTestRuns );

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
				if (!subState)
					yield break;

				var methods = subState!.GetType().GetMethods( BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic );

				foreach (var m in methods)
				{
					if (m.GetCustomAttribute<StateTestAttribute>() != null)
						yield return m.Name;
				}
			}
		}
#endif
	}
}