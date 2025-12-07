namespace Flexy.GameFlow
{
	[RequireComponent(typeof(GameContext))]
	public class GameStage: State, IService
	{
		[SerializeField]			AssetRef<State>	_mainStateRef;
		[SerializeField] protected	Transform		_statesContainer = null!;

		internal readonly	Dictionary<AssetRef<State>, State>	_stateInstances	= new(32);

		public		GameContext			Context				{get; private set;} = null!;
		
		public		Service_GameFlow	Flow				=> _node.Graph.Service;
		public		Boolean				AtStageRoot			=> _node.TransitionRoot.ActiveNode.State == this;
		
		public		void				Init				( GameContext? parentContext )	
		{
			Debug.Log( $"[GameStage] {name} - Spawned", this );
		
			Context = gameObject.GetComponent<GameContext>();
		
			if (parentContext != null)
				Context.SetParent( parentContext );
			
			// Force awake stage and all components on it
			gameObject.SetActive(true);
			gameObject.SetActive(false);
		}
		public		void				OrderedInit			( GameContext ctx ) { }
		
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
		protected internal override		Boolean				TryGoBack				( ) => false;
		protected internal override		State				InstantiateSubState		( State prefab )	
		{
			var state = prefab.InstantiateInactive(_statesContainer); 
			_stateInstances[prefab.PrefabRef] = state;
			return state;
		}
		protected internal override		void				DestroySubState			( State instance )	
		{
			Destroy(instance.gameObject);
			_stateInstances.Remove(instance.PrefabRef);
		}
		
#if UNITY_EDITOR
		[RuntimeInspectorGui( Repaint = true )]
		internal void DrawRuntimeUI( ) => Graph.DrawRuntimeUI();
#endif
	}
}