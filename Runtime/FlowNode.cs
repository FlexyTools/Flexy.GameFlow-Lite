namespace Flexy.GameFlow;

public class FlowNode
{
	public	FlowGraph		Graph			{get; internal set;} = null!;

	public	AssetRef<State>	StateRef		{get; internal set;}
	public	AssetRef<State>	MainSubStateRef {get; internal set;}
	public	State			State			{get; internal set;} = null!; // View of logical node
	
	public	Boolean			WasShown		{get; internal set;} // used to call OnShow in case first show will be BackShow
	public	Boolean			IsLocked		{get; internal set;} // we can not go back from locked node only close
	public	Object?			OpenParams		{get; internal set;} // parameters state opened with
	public	Object?			UserNodeData	{get; internal set;} // optional user data that will survive state unload and reload
	
	public	FlowNode?		PrevSibling		{get; internal set;}
	public	FlowNode?		NextSibling		{get; internal set;}
	public	FlowNode		Back			{get; internal set;} = null!;
	public	FlowNode?		Forward			{get; internal set;}
	public	FlowNode		Parent			{get; internal set;} = null!;
	public	FlowNode?		FirstChild		{get; internal set;}
	
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
	public static	FlowNode	GetLastSibling		( this FlowNode node )	
	{
		for (;node.NextSibling != null; node = node.NextSibling);
		return node;
	}
	public static	FlowNode?	GetLastSiblingOrNull( this FlowNode? node )	
	{
		if (node == null)
			return null;
	
		return GetLastSibling(node);
	}
}