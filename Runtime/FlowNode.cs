namespace Flexy.GameFlow;

public class FlowNode
{
	public	FlowGraph		Graph			{get; internal set;} = null!;

	public	AssetRef<State>	StateRef		{get; internal set;}
	public	AssetRef<State>	MainSubStateRef {get; internal set;}
	public	State			State			{get; internal set;} = null!; // View of logical node
	
	public	Boolean			FullyInited		{get; internal set;} // if it is false in OnShow than first show came from BackShow
	public	Object?			OpenParams		{get; internal set;} // parameters state opened with
	public	Object?			StateData		{get; internal set;} // optional state data can be stored by state implementation
	public	Object?			UserData		{get; internal set;} // optional user data for (link additional data from outside the state)
	
	public	FlowNode		Parent			{get; internal set;} = null!;
	public	FlowNode?		FirstChild		{get; internal set;}
	public	FlowNode?		PrevSibling		{get; internal set;}
	public	FlowNode?		NextSibling		{get; internal set;}
	public	FlowNode?		Back			{get; internal set;}
	public	FlowNode?		Forward			{get; internal set;}
	
	public	StateHandle		Handle			=> new(this);
	public	Boolean			IsValid			=> Graph.Root == this || Back?.Forward == this;
	public	Boolean			IsOpened		=> IsValid;
	public	Boolean			IsShowed		=> IsOpened && State && State.Node == this && State.gameObject.activeInHierarchy;
	
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

	public override	String	ToString		( )	
	{
		return $"{(IsShowed ? "■ " : "□ ")} {State.name} {(OpenParams != null ? "op:" + OpenParams : "")}";
	}

	public	StateHandle		Close			( )	
	{
		Graph.RemoveNode(this);
		return Handle;
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