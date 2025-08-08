namespace Flexy.GameFlow;

public class FlowNode
{
	public	FlowGraph	Graph			{get; internal set;}

	public	Int32		Uid				{get; internal set;}
	public	Boolean		WasShown		{get; internal set;} // used to call OnShow in case first show will be BackShow
	public	Boolean		IsLocked		{get; internal set;}
	public	Object		OpenParams		{get; internal set;}
	public	State		State			{get; internal set;}
	
	public	FlowNode?	PrevSibling		{get; internal set;}
	public	FlowNode?	NextSibling		{get; internal set;}
	public	FlowNode?	Back			{get; internal set;}
	public	FlowNode?	Forward			{get; internal set;}
	public	FlowNode?	Parent			{get; internal set;}
	public	FlowNode?	FirstChild		{get; internal set;}
	
	public	StateHandle	Handle			=> new(this);
	public	Boolean		IsValid			=> Graph.Root == this || Back?.Forward == this;
	public	Boolean		IsOpened		=> IsValid;
	public	Boolean		IsShowed		=> IsOpened && State.gameObject.activeInHierarchy;

	public	FlowNode	GameStageNode	
	{
		get
		{
			for (var current = this; current != null; current = current.Parent)
				if (current.State is GameStage gs)
					return current;
						
			return null;
		}
	}

	public override	String	ToString		( )	
	{
		return $"{Uid:D3} {(IsShowed ? "■ " : "□ ")} {State.name.Replace( "State", "", StringComparison.OrdinalIgnoreCase ).Trim('_')} {(OpenParams != null ? "op:" + OpenParams : "")}";
	}

	public	StateHandle		Close			( )	
	{
		Graph.RemoveNode(this);
		return Handle;
	}
}

public static class FlowNodeExt
{
	public static	FlowNode?	GetLastSibling	( this FlowNode? node )	
	{
		if (node == null) 
			return null;
		
		for (;node.NextSibling != null; node = node.NextSibling);
		return node;
	}
}