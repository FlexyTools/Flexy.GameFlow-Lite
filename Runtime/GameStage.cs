namespace Flexy.GameFlow
{
	public class GameStage: State, IService
	{
		[SerializeField] AssetRef<State>	_mainStateRef;
		[SerializeField] Transform			_statesContainer;

		internal readonly	Dictionary<AssetRef<State>, State>	_stateInstances	= new(32);

		public		Service_GameFlow	Flow				{get; private set;}
		public		GameContext			Context				{get; private set;}
		
		public		Boolean				AtStageRoot			=> _graph.MainLineActive.State == this;
		
		public		void				Init				( Service_GameFlow flow, GameContext? parentContext )	
		{
			Debug.Log( $"[GameStage] {name} - Spawned", this );
		
			Flow = flow;
		
			var ctx = gameObject.GetComponent<GameContext>();

			if( !ctx )
				// Create new context for GameState
				ctx = gameObject.AddComponent<GameContext>();
		
			ctx.SetParent( parentContext );
		
			Context = ctx;
			
			// Force awake stage and all components on it
			gameObject.SetActive(true);
			gameObject.SetActive(false);
		}
		public		void				OrderedInit			( GameContext ctx) { }
		
		public		void				MoveToLoadedScene	( Scene loadedScene )	
		{
			SceneManager.MoveGameObjectToScene( gameObject, loadedScene );
			GameStage.transform.SetSiblingIndex(0);
		}
		public		void				MoveToServiceScene	( )						
		{
			SceneManager.MoveGameObjectToScene( gameObject, Graph.Service.gameObject.scene );
		}
		
		protected override void			OnShow				( )						
		{
			if (Node.FirstChild == null) 
				OpenMainState();
		}
		
		protected internal override		AssetRef<State>		MainSubStateRef			=> _mainStateRef;
		protected internal override		Transform			GetSubStatesContainer	( ) => _statesContainer;

#if UNITY_EDITOR
		[RuntimeInspectorGui( Repaint = true )]
		internal void DrawRuntimeUI( ) => Graph.DrawRuntimeUI();
#endif
	}
}