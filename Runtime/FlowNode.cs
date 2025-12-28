namespace Flexy.GameFlow;

public class FlowNode
{
	private TransitionRoot? _transitionRoot;
	
	public	FlowGraph		Graph			{get; internal init;} = null!;
	public	State			State			{get; internal init;} = null!; // View of logical node
	
	public	Boolean			FullyInited		{get; internal set;} // if it is false in OnShow than first show came from BackShow
	public	Boolean			ChildrenShowed	{get; internal set;}
	public	Object?			OpenParams		{get; internal set;} // parameters state opened with
	public	Object?			StateData		{get; set;} // optional state data can be stored by state implementation
	public	Object?			UserData		{get; set;} // optional user data for (link additional data from outside the state)
	
	public	FlowNode?		Back			{get; internal set;}
	public	FlowNode?		Forward			{get; internal set;}
	public	FlowNode?		PrevSibling		{get; internal set;}
	public	FlowNode?		NextSibling		{get; internal set;}
	
	public	FlowNode		Parent			{get; internal set;} = null!;
	public	FlowNode?		FirstChild		{get; internal set;}
	
	public	Boolean			IsOpened			=> Graph.Root == this || (PrevSibling == null ? Parent.FirstChild == this : PrevSibling.NextSibling == this);
	public	Boolean			IsShowing			=> State && State.Node == this && State.gameObject.activeSelf;
	public	Boolean			IsOpenedAndShowing	=> IsOpened && IsShowing;
	public	TransitionRoot	TransitionRoot		=> _transitionRoot ?? Parent.TransitionRoot;
	
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
		return $"{(IsShowing ? "■ " : "□ ")} {State.name} {(OpenParams != null ? "op:" + OpenParams : "")}";
	}
	public	FlowNode		Close				( )	
	{
		Graph.RemoveNode(this);
		return this;
	}
	public	void			SpawnTransitionRoot	( )	
	{
		_transitionRoot = new TransitionRoot
		{
			_node		= this,
			_tipNode	= this,
			_activeNode	= this
		};
		
		_transitionRoot.SwitchStatesAsyncInfiniteLoop().Forget();
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
}