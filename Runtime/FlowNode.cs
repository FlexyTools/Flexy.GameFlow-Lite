namespace Flexy.GameFlow;

public class FlowNode
{
	private TransitionHost? _transitionHost;
	
	public	FlowGraph		Graph			{get; internal init;} = null!;
	public	State			State			{get; internal init;} = null!; // View of logical node
	
	public	Boolean			ChildrenShowed	{get; internal set;}
	public	Object?			OpenParams		{get; set;} // parameters state opened with
	public	Object?			StateData		{get; set;} // optional state data can be stored by state implementation
	public	Object?			UserData		{get; set;} // optional user data for (link additional data from outside the state)
	
	public	FlowNode?		Back			{get; internal set;}
	public	FlowNode?		Forward			{get; internal set;}
	public	FlowNode?		PrevSibling		{get; internal set;}
	public	FlowNode?		NextSibling		{get; internal set;}
	
	public	FlowNode		Parent			{get; internal set;} = null!;
	public	FlowNode?		FirstBaseChild	{get; internal set;}
	
	public	Boolean			IsOpened			=> Graph.Root == this || (PrevSibling == null ? Parent.FirstBaseChild == this : PrevSibling.NextSibling == this);
	public	Boolean			IsShowing			=> State && State.Node == this && State.gameObject.activeSelf;
	public	Boolean			IsOpenedAndShowing	=> IsOpened && IsShowing;
	public	TransitionHost	TransitionHost		=> _transitionHost ?? Parent.TransitionHost;
	public	FlowNode		GameStageNode		
	{
		get
		{
			for (var current = this; current != null; current = current.Parent)
				if (current.State is GameStage gs)
					return current;
						
			throw new InvalidOperationException("GameStage absent in state tree");
		}
	}

	internal	AssetRef<State>		StateRef;
	internal	AssetRef<State>		MainSubStateRef;

	public override	String	ToString			( )	
	{
		return $"{(IsShowing ? "■ " : "□ ")} {State.name} {(OpenParams != null ? $"({OpenParams})" : "")}";
	}
	public	FlowNode?		OpenMainSubState	( Object? openParams = null )	
	{
		if (MainSubStateRef.IsNone)
			return default;
		
		Debug.Log( $"[{State.name}] => Open Main State: {Graph.Flow.GetRefType(MainSubStateRef).Name}" );
		
		if (FirstBaseChild == null)
			// main substate never was opened yet so just open it
			return Graph.Open( MainSubStateRef, this, openParams, this );
			
		// main substate is somewhere in history so just return to it
		Graph.RemoveNodesUpTo( FirstBaseChild.GetLastSibling(), FirstBaseChild, openParams );
		return FirstBaseChild;
	}
	public	FlowNode		Close				( )	
	{
		Graph.RemoveNode(this);
		return this;
	}
	public	void			SpawnTransitionRoot	( )	
	{
		_transitionHost = new TransitionHost
		{
			_node		= this,
			_tipNode	= this,
			_activeNode	= this
		};
		
		_transitionHost.SwitchStatesAsyncInfiniteLoop().Forget();
	}
	public async UniTask<T>	WaitResult<T>		( )	
	{
		if (State is not IStateWithResult<T> swr)
			throw new InvalidOperationException($"Node state {State.GetType().Name} dont implement IStateWithResult<{typeof(T).Name}>");
		
		while (IsOpened)
			await UniTask.NextFrame(PlayerLoopTiming.LastUpdate);
		
		return swr.GetResult(this);
	}
}

public static class FlowNodeExt
{
	private static readonly		HashSet<FlowNode>	_tempNodeSet = new();

	public static	FlowNode	GetLastSibling			( this FlowNode node )	
	{
		for (;node.NextSibling != null; node = node.NextSibling);
		return node;
	}
	public static	FlowNode?	GetLastSiblingOrNull	( this FlowNode? node )	
	{
		if (node == null)
			return null;
	
		return GetLastSibling(node);
	}
	public static	Boolean		IsInForwardOf			( this FlowNode node, FlowNode other )	
	{
		for (var iter = node.Back; iter != null; iter = iter.Back)
		{
			if (iter == other)
				return true;
		}
		
		return false;
	}
	public static	FlowNode?	FindNodeBackwards<T>	( this FlowNode? node )	where T : State	
	{
		for (;node is { State: not T }; node = node.Back);
		
		return node;
	}
	public static	FlowNode?	FindNodeForward<T>		( this FlowNode? node )	where T : State	
	{
		for (;node is { State: not T }; node = node.Forward);
		
		return node;
	}
	public static	T?			FindStateBackwards<T>	( this FlowNode? node )	where T : State	
	{
		return (T?)FindNodeBackwards<T>(node)?.State;
	}
	public static	T?			FindStateForward<T>		( this FlowNode? node )	where T : State	
	{
		return (T?)FindNodeForward<T>(node)?.State;
	}
	
	public static	FlowNode	FindNearestCommonParent	( FlowNode nodeA, FlowNode nodeB )	
	{
		var set = _tempNodeSet;
		set.Clear();

		if (nodeA == nodeB)
			return nodeA.Parent;

		for ( ; nodeA != null; nodeA = nodeA.Parent)
			set.Add( nodeA );

		for ( ; nodeB != null; nodeB = nodeB.Parent)
			if (set.Contains( nodeB ))
				return nodeB;

		throw new InvalidOperationException("Graph broken, can not find common parent, it must be at least one common parent -> Root of the graph ");
	}
	public static	FlowNode	FindNearestCommonBack	( FlowNode nodeA, FlowNode nodeB )	
	{
		var set = _tempNodeSet;
		set.Clear();

		for ( ; nodeA != null; nodeA = nodeA.Back!)
			set.Add( nodeA );

		for ( ; nodeB != null; nodeB = nodeB.Back!)
			if (set.Contains( nodeB ))
				return nodeB;

		throw new InvalidOperationException("Graph broken, can not find common history parent, it must be at least one common parent -> Root of the graph ");
	}
}