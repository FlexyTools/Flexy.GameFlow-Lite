namespace Flexy.GameFlow;

public record struct LibCtx ( FlowNode Src )
{
	public Service_GameFlow	FlowService		=> Src.Graph.Service;

	public State.Opener		GetByUid		( String id )					=> FlowService.GetOpener_ById( Src, id );
	public State.Opener		GetState<T>		( )	where T: State				=> FlowService.GetOpener_ByStateType<T>( Src );
	public T				GetOpener<T>	( )	where T: struct, IOpener	=> FlowService.GetOpener_ByOpenerType<T>( Src );
	
	public static LibCtx	FromWidget		( MonoBehaviour w )				
	{
		var panel = w.GetComponentInParent<State>();
		return new( panel.Node );
	}
	
	public static implicit operator  LibCtx( State panel ) => new ( panel.Node );
}