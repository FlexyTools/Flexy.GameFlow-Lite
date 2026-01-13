namespace Flexy.GameFlow
{
	[HelpURL("https://github.com/FlexyTools/Flexy.Docs/blob/main/Framework/Flexy.GameFlow/ScriptingApi/State.md")]

	public abstract class State : BindableBehaviour
	{
		[SerializeField] FlexyEvent		_showing;
		[SerializeField] FlexyEvent		_hiding;
		
		internal	FlowGraph			_graph = null!;
		internal	FlowNode			_node  = null!;
		internal	AssetRef<State>		_prefabRef;
		internal	State?				_owner;
		
		public		FlowGraph			Graph			=> _graph;
		public		FlowNode			Node			=> _node;
		public		AssetRef<State>		PrefabRef		=> _prefabRef;
		
		public		Object?				OpenParams		=> _node.OpenParams;
		
		public		Boolean				IsOpened		=> _node.IsOpened;
		public		Boolean				IsShowed		=> _node.IsShowing;
		public		Boolean				AnySubStateOpened=> _node.FirstBaseChild != null;
		
		public		GameStage			GameStage		=> this as GameStage ?? (GameStage)_node.GameStageNode.State;
		
		public			FlowNode?	OpenMainSubState	( Object? openParams = null ) => _node.OpenMainSubState(openParams);
		[Callable] public	void	Close				( ) => _node.Close();
		public				void	CloseAndDestroy		( )									
		{
			if (_node == null)
			{
				Graph.DestroyState(this);
				return;
			}
		
			Close();
			
			if (_node.IsShowing)
				DestroyWhenStateWillHide(_node).Forget();
			else
				Graph.DestroyState(this);
				
			return;

			static async UniTaskVoid DestroyWhenStateWillHide( FlowNode node )
			{
				var state = node.State;
				await UniTask.WaitWhile( () => state.gameObject.activeSelf );
				node.Graph.DestroyState(state);
			}
		}
		public			FlowNode?	CloseSubStates		( Boolean closeMainState = false, Boolean closeCurrent = true, Object? overrideOpenParams = null )	
		{
			if (_node.FirstBaseChild == null)
				return null;
		
			var target = _node;
		
			if (!closeMainState && !_node.MainSubStateRef.IsNone)
			{
				target = _node.FirstBaseChild;
			}
			else
			{
				_node.MainSubStateRef = default;
			}
		
			var currentNode = _node.FirstBaseChild.GetLastSibling();
		
			if (!closeCurrent)
				currentNode = currentNode.Back;
		
			Graph.RemoveNodesUpTo( currentNode, target, overrideOpenParams );
			return _node;
		}

		public				void	RebindAllHierarchy	( )	
		{
			foreach (var bb in gameObject.GetComponentsInChildren<BindableBehaviour>())
			{
				if (bb.gameObject == gameObject)	bb.MakeBindReadyAndRebindAll();
				else								bb.RebindAll();
			}
		}
		public override		String	ToString			( ) => _node.ToString();
		
		protected internal virtual	AssetRef<State>		MainSubStateRef			=> default;
		protected internal virtual	Boolean				TryGoBack				( )	=> true;
		protected internal virtual	State				InstantiateSubState		( State prefab, String tag )	=> throw new InvalidOperationException($"State {GetType().Name} not designed to have substates");
		protected internal virtual	void				DestroySubState			( State state )					=> throw new InvalidOperationException($"State {GetType().Name} not designed to have substates");
		protected internal 			State				InstantiateState		( State statePrefab, String tag )	
		{
			var state = InstantiateSubState(statePrefab, tag);
			state._prefabRef = statePrefab._prefabRef;
			state._owner = this;
			state._graph = _graph;
			
			state.NicifyName();
			
			return state;
		}
		protected internal			void				NicifyName				( )									
		{
			try
			{
				var niceName = name.Replace( "(Clone)", "" ).Replace("_", " ").Trim('_').Trim(' ');
				var spaceIndex = niceName.IndexOf(' ');
				
				if (spaceIndex != -1 && spaceIndex < niceName.Length - 1)
					niceName = niceName.Insert(spaceIndex, "]").Insert(0, "[");
				
				name = niceName;
			}
			catch (Exception ex) { Debug.LogException(ex); }
		}	
		
		internal async		UniTask	DoShow				( )	
		{ 
			try						{ await OnShow(); }
			catch ( Exception ex )	{ Debug.LogException( ex ); }
			
			if (!gameObject.activeSelf)
				gameObject.SetActive(true);
			
			_showing.Raise(this);
		}
		internal async		UniTask	DoForwardHide		( )	
		{
			_hiding.Raise(this);
			
			try						{ await OnForwardHide(); }
			catch ( Exception ex )	{ Debug.LogException(ex); }
			
			if (gameObject.activeSelf)
				gameObject.SetActive(false);
		}
		internal async		UniTask	DoBackShow			( )	
		{
			try						{ await OnBackShow(); }
			catch ( Exception ex )	{ Debug.LogException(ex); }
			
			if (!gameObject.activeSelf)
				gameObject.SetActive(true);
				
			_showing.Raise(this);
		}
		internal async		UniTask	DoHide				( )	
		{
			_hiding.Raise(this);
		
			try						{ await OnHide(); }
			catch ( Exception ex )	{ Debug.LogException(ex); }
			
			if (gameObject.activeSelf)
				gameObject.SetActive(false);
		}
		
		internal			void	DoFirstChildShow	( FlowNode node )	
		{
			try						{ OnFirstChildShow(); }
			catch ( Exception ex )	{ Debug.LogException( ex ); }
			
			node.ChildrenShowed = true;

			//Debug.Log( $"[TransitionOperation] {node} FirstChildShow" );
		}
		internal			void	DoLastChildHide		( FlowNode node )	
		{
			//Debug.Log( $"[TransitionOperation] {node} LastChildHide" );
		
			node.ChildrenShowed = false;
			
			try						{ OnLastChildHide(); }
			catch ( Exception ex )	{ Debug.LogException( ex ); }
		}
		
		protected virtual	UniTask	OnShow				( )	{ gameObject.SetActive(true);	return default; }
		protected virtual	UniTask	OnForwardHide		( )	{ gameObject.SetActive(false);	return default; }
		protected virtual	UniTask	OnBackShow			( )	{ gameObject.SetActive(true);	return default; }
		protected virtual	UniTask	OnHide				( )	{ gameObject.SetActive(false);	return default; }
		
		protected virtual	void	OnFirstChildShow	( )	{ }
		protected virtual	void	OnLastChildHide		( )	{ }
		
		public record struct Opener	( OpenCtx Ctx ) : IOpenerB
		{
			public	FlowNode	Open	( ) => Ctx.Open();
		}
	}
	
	public interface IStateWithResult<out T> { T GetResult( FlowNode node ); }
	public record struct ResultNode<T>( FlowNode Node ){ public UniTask<T> WaitResult( ) => Node.WaitResult<T>(); }
	
	[AttributeUsage(AttributeTargets.Method)]
	public class StateTestAttribute: Attribute {}
}