namespace Flexy.GameFlow
{
	[HelpURL("https://github.com/FlexyTools/Flexy.Docs/blob/main/Framework/Flexy.GameFlow/ScriptingApi/GameStage.md")] 

	[RequireComponent(typeof(GameContext))]
	public class GameStage: State, IService
	{
		[SerializeField]			AssetRef<State>	_mainStateRef;
		[SerializeField] protected	Transform		_statesContainer = null!;

		internal readonly	Dictionary<AssetRef<State>, State>	_statesCache	= new(32);
		
		public		GameContext			Context				{get; private set;} = null!;
		
		public		void				MoveToLoadedScene	( Scene loadedScene )	
		{
			SceneManager.MoveGameObjectToScene( gameObject, loadedScene );
			GameStage.transform.SetSiblingIndex(0);
		}
		public		void				MoveToServiceScene	( )						
		{
			SceneManager.MoveGameObjectToScene( gameObject, Graph.Flow.gameObject.scene );
		}
		
		internal	void				PreInit				( GameContext? parentContext )	
		{
			Debug.Log( $"[GameStage] {name} - Spawned", this );
		
			Context = gameObject.GetComponent<GameContext>();
		
			if (parentContext != null)
				Context.SetParent( parentContext );
			
			// Force awake stage and all components on it specifically GameContext
			gameObject.SetActive(true);
			gameObject.SetActive(false);
		}
		void		IService.			OrderedInit			( GameContext ctx ) { }
		
		protected override	UniTask		OnShow				( )	
		{
			if (Node.FirstBaseChild == null) 
				OpenMainSubState();
				
			return default;
		}
		protected override	UniTask		OnForwardHide		( )	=> OnHide();
		protected override	UniTask		OnBackShow			( )	=> OnShow();

		protected internal override		AssetRef<State>		MainSubStateRef			=> _mainStateRef;
		protected internal override		Boolean				TryGoBack				( ) => false;

		protected internal override		State				InstantiateSubState		( State prefab, String tag )
		{
			var state = prefab.InstantiateInactive(_statesContainer); 
			_statesCache[prefab.PrefabRef] = state;
			return state;
		}
		protected internal override		void				DestroySubState			( State state )				
		{
			_statesCache.Remove(state.PrefabRef);
			Destroy(state.gameObject);
		}
		
#if UNITY_EDITOR
		[RuntimeInspectorGui( Repaint = true )]
		internal void DrawRuntimeUI( ) => Graph.DrawRuntimeUI();
#endif
	}
}