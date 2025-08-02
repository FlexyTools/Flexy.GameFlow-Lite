namespace Flexy.GameFlow;

public class FlowNode
{
	public	FlowGraph	Graph;

	public	Int32		Uid				{get; internal set;}
	public	Boolean		WasShowed		{get; internal set;} // used to call OnShow in case first show will be BackShow
	public	Boolean		IsLocked		{get; internal set;}
	public	Boolean		IsOpened		{get; internal set;}
	public	Object		OpenParams		{get; internal set;}
	public	State		State			{get; internal set;}

	public	FlowNode?	PrevSibling		{get; internal set;}
	public	FlowNode?	NextSibling		{get; internal set;}
	public	FlowNode?	Back			{get; internal set;}
	public	FlowNode?	Forward			{get; internal set;}
	public	FlowNode?	Parent			{get; internal set;}
	public	FlowNode?	FirstChild		{get; internal set;}
	
	public	StateHandle	Handle			=> new(Uid, this);
	public	Boolean		IsActive		=> IsOpened && State.gameObject.activeInHierarchy;
	public	FlowNode?	LeftNode		=> PrevSibling ?? Back ?? Parent;
	
	public override	String	ToString		( )	
	{
		return $"{Uid:D3} {(IsActive ? "■ " : "□ ")} {State.name.Replace( "State", "", StringComparison.OrdinalIgnoreCase ).Trim('_')} {(OpenParams != null ? "op:" + OpenParams : "")}";
	}

	public	StateHandle		Close			( )	
	{
		var h = Handle;
		Graph.RemoveNode(Handle);
		return h;
	}

	public	FlowNode		GetLastSibling	( )	
	{
		var node = this;
		for (;node.NextSibling != null; node = node.NextSibling);
		return node;
	}
}