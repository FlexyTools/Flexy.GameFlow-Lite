namespace Flexy.GameFlow
{
	public class GameStage: State
	{
		[FormerlySerializedAs("_rootStateRef")]
		[SerializeField] AssetRef<State>	_mainStateRef;
		[SerializeField] Transform			_statesContainer;

		public		GameContext			Context				{get; private set;}
		public		FlowGraph			Graph				=> _node.Graph;
		public		FlowNode			Node				=> _node;
		public		StateHandle			RootHandle			=> _node.Handle;

		public		State				CurrentState		=> Graph.MainLineTip.State;
		public		State				ActiveState			=> Graph.MainLineActive.State;
		public		Boolean				AtStageRoot			=> _node.IsActive;

		public		AssetRef<State>		MainStateRef		=> _mainStateRef;
		public		State				MainState			=> _node.FirstChild?.State;
		public		StateHandle			MainHandle			=> _node.FirstChild?.Handle ?? default;
		
		public		Transform			StatesContainer		=> _statesContainer;
		
		internal void SetContext(GameContext ctx) => Context = ctx;
		
		public		StateHandle			OpenMainState		( Object openParams = null )
		{
			if( _node.FirstChild == null )
			{
				// main substate never was opened yet so just open it
				var handle = Graph.Open( _mainStateRef, this, openParams, parent:_node );
				Debug.Log( $"[GameStage] {name} => Open Root State: {handle.State.name}" );
			}
			else
			{
				// loader is somewhere in history so just return to it
				Debug.Log( $"[GameStage] {name} => Open Main State: {_node.State.name}" );
				Graph.RemoveNodesUpTo( _node, openParams );
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
			SceneManager.MoveGameObjectToScene( gameObject, Graph.Service.gameObject.scene );
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