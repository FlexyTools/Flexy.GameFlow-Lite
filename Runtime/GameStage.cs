namespace Flexy.GameFlow
{
	public class GameStage: State
	{
		[FormerlySerializedAs("_rootStateRef")]
		[SerializeField] AssetRef<State>	_mainStateRef;
		[SerializeField] Transform			_statesContainer;

		public		GameContext			Context				{get; private set;}
		public		GameFlowService		FlowService			=> _node.Graph.Service;
		public		FlowNode			Node				=> _node;
		public		StateHandle			RootHandle			=> _node.GetHandle();

		public		State				CurrentState		=> _node.Graph.CurrentStateNode.State;
		public		State				ActiveState			=> _node.Graph.ActiveStateNode.State;
		public		Boolean				AtStageRoot			=> _node.Graph.CurrentStateNode == _node;

		public		AssetRef<State>		MainStateRef		=> _mainStateRef;
		public		State				MainState			=> _node.Forward?.State;
		public		StateHandle			MainHandle			=> _node.Forward?.GetHandle( ) ?? default;
		
		public		Transform			StatesContainer		=> _statesContainer;
		
		public		StateHandle			OpenNewState		( AssetRef<State> stateRef, Object openParams = null )
		{
			var state		= _node.Graph.GetLoadedState( stateRef, this, default );

			Debug.Log( $"[GameStage] {name} => Open State: {state.name}" );

			if( state == MainState )	
				return OpenMainState();

			var node		= _node.Graph.AddNode( state, openParams );

			return node.GetHandle( );
		}
		public		StateHandle			OpenMainState		( Object openParams = null )
		{
			if( _node == null )
			{
				// main substate never was opened yet so just open it
				var rootState = _node.Graph.GetLoadedState( _mainStateRef, this, default );
				Debug.Log( $"[GameStage] {name} => Open Root State: {rootState.name}" );
				_node = _node.Graph.AddNode( rootState, openParams, true );
			}
			else
			{
				// loader is somewhere in history so just return to it
				Debug.Log( $"[GameStage] {name} => Open Main State: {_node.State.name}" );
				_node.Graph.RemoveNodesUpTo( _node, openParams );
			}

			return RootHandle;
		}
		
		public		void				RemoveFromHistoryAfterClose	( )
		{
			Do( gameObject ).Forget();
			static async UniTask Do	( GameObject go )
			{ 
				await UniTask.WaitWhile( () => go.activeInHierarchy );
				Destroy( go );
			}
		}

		public		void				MoveToLoadedScene	( Scene loadedScene )		
		{
			SceneManager.MoveGameObjectToScene( gameObject, loadedScene );
			GameStage.transform.SetSiblingIndex(0);
		}
		public		void				MoveToServiceScene	( )							
		{
			SceneManager.MoveGameObjectToScene( gameObject, FlowService.gameObject.scene );
		}
		
		protected override	void		OnShow	( )		
		{
			Debug.Log( $"[GameStage] Init: {name}" );
			name	= "[GS] " + name;
		
			// Setup GameContext
			{
				var ctx = gameObject.GetComponent<GameContext>( );

				if( !ctx )
					// Create new context for GameState
					ctx = gameObject.AddComponent<GameContext>( );
			
				if (OpenParams is GameContext gc)	
					ctx.SetParent( gc );
					
				ctx.SetService( this );
			
				Context = ctx;
			}
		
			OpenMainState();
		
			base.OnShow();
		}
		
		protected internal virtual	Boolean		TryGoBack				()	=> false;
		protected internal override	Transform	GetSubStatesContainer	()
		{
			return _statesContainer;
		}

		public record struct Opener( OpenCtx Ctx ) : IOpener
		{
			public	StateHandle	Open	( GameContext parentContext = null ) => Ctx.Open(parentContext);
		}
		
#if UNITY_EDITOR
		[RuntimeInspectorUI( Repaint = true )]
		internal void DrawRuntimeUI( ) => _node.Graph.DrawRuntimeUI();
#endif
	}
}