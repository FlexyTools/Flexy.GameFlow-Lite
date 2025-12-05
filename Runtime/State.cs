namespace Flexy.GameFlow
{
	public abstract class State : BindableBehaviour
	{
		[SerializeField] FlexyEvent		_showing;
		[SerializeField] FlexyEvent		_hiding;
		
		internal	FlowGraph			_graph = null!;
		internal	FlowNode			_node  = null!; // Can be null only when unused but loaded, so no one can access it in this state
		internal	AssetRef<State>		_prefabRef;
		internal	State?				_owner;
		
		public		FlowGraph			Graph			=> _graph;
		public		FlowNode			Node			=> _node;
		public		AssetRef<State>		PrefabRef		=> _prefabRef;
		
		public		Object?				OpenParams		=> _node.OpenParams;
		public		StateHandle			Handle			=> new(_node);
		
		public		Boolean				IsOpened		=> _node.IsOpened;
		public		Boolean				IsShowed		=> _node.IsShowed;
		public		Boolean				AnySubStateOpened=> _node.FirstChild != null;
		
		public		State?				MainSubState	=> _node.FirstChild?.State;
		public		StateHandle			MainSubHandle	=> _node.FirstChild?.Handle ?? default;
		public		GameStage			GameStage		=> this as GameStage ?? (GameStage)_node.GameStageNode.State;
		
		protected internal virtual	AssetRef<State>		MainSubStateRef			=> default;
		protected internal virtual	Boolean				TryGoBack				( )	=> true;
		protected internal virtual	State				InstantiateSubState		( State prefab )	=> throw new InvalidOperationException($"State {GetType().Name} not designed to have substates");
		protected internal virtual	void				DestroySubState			( State instance )	=> throw new InvalidOperationException($"State {GetType().Name} not designed to have substates");
		protected internal 			State				InstantiateState		( State statePrefab )		
		{
			var state = InstantiateSubState(statePrefab);
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
					name = niceName.Insert(spaceIndex, "]").Insert(0, "[");
			}
			catch (Exception ex) { Debug.LogException(ex); }
		}	
		
		internal			void	DoShow				( )	
		{ 
			try						{ OnShow(); }
			catch ( Exception ex )	{ Debug.LogException( ex ); }
			
			if (ReadyForBind)
				try						{ RebindAllHierarchy(); }
				catch ( Exception ex )	{ Debug.LogException( ex ); }
				
			_showing.Raise(this);
		}
		internal			void	DoFwdHide			( )	
		{
			try						{ OnFwdHide(); }
			catch ( Exception ex )	{ Debug.LogException(ex); }
		}
		internal			void	DoBackShow			( )	
		{
			try						{ OnBackShow(); }
			catch ( Exception ex )	{ Debug.LogException(ex); }
		}
		internal			void	DoHide				( )	
		{
			try						{ OnHide(); }
			catch ( Exception ex )	{ Debug.LogException(ex); }
			
			_hiding.Raise(this);
			_node = null!;
		}
		
		internal			void	DoFirstChildShow	( FlowNode node )	
		{
			try						{ OnFirstChildShow(); }
			catch ( Exception ex )	{ Debug.LogException( ex ); }
			
			node.ChildrenShowed = true;
		}
		internal			void	DoLastChildHide		( FlowNode node )	
		{
			node.ChildrenShowed = false;
			
			try						{ OnLastChildHide(); }
			catch ( Exception ex )	{ Debug.LogException( ex ); }
		}
		
		protected virtual	void	OnShow				( )	{ }
		protected virtual	void	OnFwdHide			( )	{ }
		protected virtual	void	OnBackShow			( )	{ }
		protected virtual	void	OnHide				( )	{ }
		
		protected virtual	void	OnFirstChildShow	( )	{ }
		protected virtual	void	OnLastChildHide		( )	{ }
		
		public		StateHandle		OpenMainState		( Object? openParams = null )		
		{
			if (MainSubStateRef.IsNone)
				return default;
		
			Debug.Log( $"[GameStage] {name} => Open Main State: {GameStage.Flow.GetRefTypeName(MainSubStateRef)}" );
		
			if (_node.FirstChild == null)
				// main substate never was opened yet so just open it
				return Graph.Open( MainSubStateRef, this, openParams, parent:_node );
			
			// main substate is somewhere in history so just return to it
			Graph.RemoveNodesUpTo( _node.FirstChild.GetLastSibling(), _node.FirstChild, openParams );
			return _node.FirstChild.Handle;
		}
		[Callable] public	void	Close				( )									
		{
			_node.Close();
		}
		public				void	CloseAndDestroy		( )									
		{
			if (_node == null)
			{
				Graph.DestroyInstance(this);
				return;
			}
		
			Close();
			DestroyWhenStateWillHide(_node).Forget();
			return;

			static async UniTaskVoid DestroyWhenStateWillHide( FlowNode node )
			{
				var state = node.State;
				await UniTask.WaitWhile( () => state.gameObject.activeSelf );
				node.Graph.DestroyInstance(state);
			}
		}
		public		StateHandle		CloseSubStates		( Boolean closeMainState = false, Object? overrideOpenParams = null )	
		{
			if (_node.FirstChild == null)
				return Handle;
		
			var target = _node;
		
			if (!closeMainState && !_node.MainSubStateRef.IsNone)
			{
				target = _node.FirstChild;
			}
			else
			{
				_node.MainSubStateRef = default;
			}
		
			Graph.RemoveNodesUpTo( _node.FirstChild.GetLastSibling(), target, overrideOpenParams );
			return Handle;
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

		public record struct Opener( OpenCtx Ctx ) : IOpenerB
		{
			public		StateHandle		Open	( ) => Ctx.Open();
		}
	}
	
	public readonly record struct StateHandle( FlowNode Node )
	{
		public		FlowNode	Node		{get;} = Node; 
		
		public 		Boolean 	IsValid		=> Node.IsValid;
		public		State		State		=> Node.State;

		public		Boolean		IsOpened	=> Node.IsOpened;
		public		Boolean		IsShowed	=> Node.IsShowed;

		public			StateHandle	Close		( ) => !IsValid ? default : Node.Close();
		public override	String		ToString	( ) => $"StateHandle {Node.State}";
	}
	
	public readonly record struct ResultHandle<T>( StateHandle Handle )
	{
		public			StateHandle	Handle		{get;}	= Handle;
		public async	UniTask<T>	WaitResult	( )		
		{
			var node	= Handle.Node;
			var result	= (IStateWithResult<T>)node.State;
			
			while (node.IsOpened || node.IsShowed)
				await UniTask.NextFrame(PlayerLoopTiming.LastUpdate);
				
			return result.GetResult();
		} 
	}
	
	public interface IStateWithResult<out T>
	{
		public T GetResult( );
	}
	
	[AttributeUsage(AttributeTargets.Method)]
	public class StateTestAttribute: Attribute {}
}