namespace Flexy.GameFlow
{
	public class GameStage: State
	{
		[FormerlySerializedAs("_rootStateRef")]
		[SerializeField] AssetRef<State>	_mainStateRef;
		[SerializeField] Transform			_statesContainer;

		internal readonly	Dictionary<AssetRef<State>, State>	_stateInstances	= new( 32 );

		public		Service_GameFlow	Service				{get; private set;}
		public		GameContext			Context				{get; private set;}
		
		public		State				CurrentState		=> Graph.MainLineTip.State;
		public		State				ActiveState			=> Graph.MainLineActive.State;
		public		Boolean				AtStageRoot			=> _node.IsShowed;

		public		AssetRef<State>		MainStateRef		=> _mainStateRef;
		public		State				MainState			=> _node.FirstChild?.State;
		public		StateHandle			MainHandle			=> _node.FirstChild?.Handle ?? default;
		
		public		Transform			StatesContainer		=> _statesContainer;

		public		void				Setup				( Service_GameFlow service, GameContext? parentContext )	
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
			if (_mainStateRef.IsNone)
				return default;
		
			Debug.Log( $"[GameStage] {name} => Open Main State: {Service.GetRefTypeName(_mainStateRef)}" );
		
			if( _node.FirstChild == null )
				// main substate never was opened yet so just open it
				return Graph.Open( _mainStateRef, this, openParams, parent:_node, isLocked:true );
			
			// loader is somewhere in history so just return to it
			Graph.RemoveNodesUpTo( _node.FirstChild.GetLastSibling(), _node.FirstChild, openParams );
			return _node.FirstChild.Handle;
		}
		public		StateHandle			CloseAllStates		( )								
		{
			if( _node.FirstChild == null )
				return Handle;
		
			Graph.RemoveNodesUpTo( _node.FirstChild.GetLastSibling(), _node );
			return Handle;
		}
		[Callable] public	void		Close				( )								
		{
			CloseAllStates();
			_node.Close();
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
		
		protected override				void		OnShow					( ) => OpenMainState();
		protected internal override		Transform	GetSubStatesContainer	( ) => _statesContainer;

#if UNITY_EDITOR
		[RuntimeInspectorGui( Repaint = true )]
		internal void DrawRuntimeUI( ) => Graph.DrawRuntimeUI();
#endif
	}
}