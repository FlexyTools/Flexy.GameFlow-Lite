namespace Flexy.GameFlow;

public record struct LibCtx ( FlowNode Src )
{
	public Service_GameFlow	FlowService		=> Src.Graph.Flow;

	public State.Opener		GetState<T>		( String? guid = null )	where T: State				=> FlowService.GetOpener_ByStateType<T>( Src, guid );
	public T				GetOpener<T>	( String? guid = null )	where T: struct, IOpener	=> FlowService.GetOpener_ByOpenerType<T>( Src, guid );
	
	public static LibCtx	FromWidget		( MonoBehaviour w )				
	{
		var panel = w.GetComponentInParent<State>();
		return new( panel.Node );
	}
	
	public static implicit operator  LibCtx( State panel ) => new ( panel.Node );
}