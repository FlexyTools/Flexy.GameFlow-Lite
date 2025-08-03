namespace Flexy.GameFlow
{
	public class GameStage: State
	{
		[FormerlySerializedAs("_rootStateRef")]
		[SerializeField] AssetRef<State>	_mainStateRef;
		[SerializeField] Transform			_statesContainer;

		public		GameFlowService		Service				{get; private set;}
		public		GameContext			Context				{get; private set;}
		
		public		State				CurrentState		=> _node.Graph.MainLineTip.State;
		public		State				ActiveState			=> _node.Graph.MainLineActive.State;
		public		Boolean				AtStageRoot			=> _node.IsShowed;

		public		AssetRef<State>		MainStateRef		=> _mainStateRef;
		public		State				MainState			=> _node.FirstChild?.State;
		public		StateHandle			MainHandle			=> _node.FirstChild?.Handle ?? default;
		
		public		Transform			StatesContainer		=> _statesContainer;

		public		void				Setup				( GameFlowService service, GameContext? parentContext )	
		{
			Service = service;
		
			var ctx = gameObject.GetComponent<GameContext>();

			if( !ctx )
				// Create new context for GameState
				ctx = gameObject.AddComponent<GameContext>();
		
			ctx.SetParent( parentContext );
			ctx.SetService( this );
		
			Context = ctx;
		}
		public		StateHandle			OpenMainState		( Object openParams = null )	
		{
			Debug.Log( $"[GameStage] {name} => Open Main State: {Service.GetRefTypeName(_mainStateRef)}" );
		
			if( _node.FirstChild == null )
				// main substate never was opened yet so just open it
				return _node.Graph.Open( _mainStateRef, this, openParams, parent:_node, isLocked:true );
			
			// loader is somewhere in history so just return to it
			_node.Graph.RemoveNodesUpTo( _node.FirstChild.GetLastSibling(), _node.FirstChild, openParams );
			return _node.FirstChild.Handle;
		}
		public		StateHandle			CloseAllStates		( )								
		{
			if( _node.FirstChild == null )
				return Handle;
		
			_node.Graph.RemoveNodesUpTo( _node.FirstChild.GetLastSibling(), _node );
			return Handle;
		}
		
		public		void				MoveToLoadedScene	( Scene loadedScene )		
		{
			SceneManager.MoveGameObjectToScene( gameObject, loadedScene );
			GameStage.transform.SetSiblingIndex(0);
		}
		public		void				MoveToServiceScene	( )							
		{
			SceneManager.MoveGameObjectToScene( gameObject, _node.Graph.Service.gameObject.scene );
		}
		
		protected override				void		OnShow					( ) => OpenMainState();
		protected internal override		Transform	GetSubStatesContainer	( ) => _statesContainer;

#if UNITY_EDITOR
		[RuntimeInspectorUI( Repaint = true )]
		internal void DrawRuntimeUI( ) => _node.Graph.DrawRuntimeUI();
#endif
	}
}