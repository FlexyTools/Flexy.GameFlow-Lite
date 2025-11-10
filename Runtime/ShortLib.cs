namespace Flexy.GameFlow;

public record struct ShortLib ( State Src )
{
	public GameFlowService	FlowSvc		=> Src.GameStage.FlowService;
	
	public State.Opener		GetByUid	( String id )					=> FlowSvc.GetOpener_FromId( id, Src );
	public State.Opener		GetState<T>	( )	where T: State				=> FlowSvc.GetOpener_FromStateType<T>( Src );
	public T				GetOpener<T>( )	where T: struct, IOpener	=> FlowSvc.GetOpener_FromOpenerType<T>( Src );
	
	public static ShortLib	FromWidget	( MonoBehaviour w )				
	{
		var panel = w.GetComponentInParent<State>();
		return new( panel );
	}
	
	public static implicit operator  ShortLib( State panel ) => new ( panel );
}