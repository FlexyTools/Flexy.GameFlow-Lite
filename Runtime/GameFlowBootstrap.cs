using System.Linq;

namespace Flexy.GameFlow
{
	[HelpURL("https://github.com/FlexyTools/Flexy.Docs/blob/main/Framework/Flexy.GameFlow/ScriptingApi/GameFlowBootstrap.md")]

	[DefaultExecutionOrder(Int16.MinValue+100)]
	public class GameFlowBootstrap : MonoBehaviour
	{
		[SerializeField]	protected Service_GameFlow		_flowService = null!;
		[SerializeField]	protected AssetRef<GameStage>	_startGameStage;
        [SerializeField]	protected AssetRef<State>[]		_additionalStates = null!;
        [Tooltip( "Optional. Ref to final state to open after bootstrap" )]
        [SerializeField]	protected AssetRef<State>		_targetState;

		private static GameFlowBootstrap? _ref;

		private				void	Awake	( )		
		{
			// Almost very first Awake in scene thanks to DefaultExecutionOrder
			var isDuplicate = (Boolean)_ref;

			if (isDuplicate)
			{
				DestroyImmediate(gameObject);
				return;
			}

			_ref = this;
			DontDestroyOnLoad(gameObject);
			
			Debug.Log( $"[GameFlowBootstrap] [Frame:{Time.frameCount}] ----------- ===========   GameFlow Bootstrap Begin   =========== -----------" );
			Boot();
			Debug.Log( $"[GameFlowBootstrap] [Frame:{Time.frameCount}] ----------- ===========   GameFlow Bootstrap End   =========== -----------" );
		}
		protected virtual	void	Boot	( )		
		{
			var gameFlow	= _flowService.InstantiateInactive(); 
			gameFlow.name	= _flowService.name;
			var gctx		= gameFlow.GetComponent<GameContext>();
			
			foreach (var launchService in gameObject.GetComponents<MonoBehaviour>())
			{
				if (launchService == this)
					continue;

				gctx.SetService(launchService);
			}
			
			gameFlow.gameObject.SetActive(true);

			var openParams	= default(Object);

			#if UNITY_EDITOR
			{
				if (!_targetState.IsNone && Core.Editor.ToolbarControls.TestCaseDropdown.TryGetTestCaseToLaunch( "State", out var testCaseName ))
				{
					Debug.Log( "" );
					Debug.Log( "" );
					Debug.Log( $"[GameFlowBootstrap] TEST LAUNCH    -    {testCaseName}" );
					Debug.Log( "" );
					Debug.Log( "" );

					var state	= _targetState.LoadAssetSync();
					var m		= state?.GetType().GetMethod( testCaseName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance );
					openParams	= m?.Invoke( state, null );
				}
			}
			#endif

			
			var stageNode	= gameFlow.Graph.Open( _startGameStage );

			foreach (var state in _additionalStates)
				gameFlow.Graph.Open( state, stageNode.GetLastSibling().State );

			if (!_targetState.IsNone)
	            gameFlow.Graph.Open( _targetState, stageNode.GetLastSibling().State, openParams );

			// Show first state synchronously
			gameFlow.Graph.Root.TransitionRoot.TransitionNow();
		}

#if UNITY_EDITOR
		[UnityEditor.InitializeOnLoad]
		static class BootstrapTestCasesProvider
		{
			static BootstrapTestCasesProvider( ) => Core.Editor.ToolbarControls.TestCaseDropdown.AddTestProvider( "State", GetTestRuns );

			private static IEnumerable<String> GetTestRuns( )
			{
				var activeScene		= SceneManager.GetActiveScene( );
				var rootGos			= activeScene.GetRootGameObjects( );
				var bootstraps		= rootGos.Select( go => go.GetComponent<GameFlowBootstrap>() ).Where( c => c is not null );
				var bootstrap		= bootstraps.FirstOrDefault();
				var subStateToOpen	= bootstrap?._targetState ?? default; 

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