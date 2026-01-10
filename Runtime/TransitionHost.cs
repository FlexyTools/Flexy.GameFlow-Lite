namespace Flexy.GameFlow;

public class TransitionHost
{
	internal	FlowNode		_node		= null!;
	internal	FlowNode		_tipNode	= null!;
	internal	FlowNode		_activeNode	= null!;
	private		Boolean			_doTransition;
	
	public		FlowNode		TipNode		=> _tipNode;
	public		FlowNode		ActiveNode	=> _activeNode;
	
	public		FlowNode		TryGoBack						( )		
	{
		if (_tipNode.Back != null && _tipNode.State.TryGoBack())
			_node.Graph.RemoveNodesUpTo(_tipNode, _tipNode.Back);

		return _tipNode;
	}
	public			void		TransitionNow					( )		
	{
		if (!_doTransition) 
			return;
		
		_doTransition = false;
		DoStateTransitions();
	}
	
	internal		void		ScheduleSwitchStates			( )		
	{
		_doTransition	= true;
	}
	internal async	UniTask		SwitchStatesAsyncInfiniteLoop	( )		
	{
		while (Application.isPlaying && _node.State)
		{
			// Change view at last update (before animations)
			// This will allow to make many changes in update and then only one view transition
			await UniTask.NextFrame( PlayerLoopTiming.LastUpdate );

			if (!_doTransition)
				continue;

			_doTransition	= false;

			try						{ DoStateTransitions(); }
			catch ( Exception ex )	{ Debug.LogException( ex ); }
		}
	}
	internal		void		DoStateTransitions				( )		
	{
		if (_activeNode == _tipNode)
			return;
	
		InstantTransition(_activeNode, _tipNode);
		
		_activeNode = _tipNode;
	}
	
	internal static	void		InstantTransition		( FlowNode prevNode, FlowNode nextNode )		
	{
		if (prevNode == nextNode)
			return;
	
		var forwards		= ComputeForwards(prevNode, nextNode);
		var commonParent	= FlowNodeExt.FindNearestCommonParent(prevNode, nextNode);

		var closingBranchNode = prevNode;
		while (closingBranchNode != commonParent)
		{
			try{ closingBranchNode.State.gameObject.SetActive(false);	} catch (Exception ex) { Debug.LogException(ex); }
			try{ NodeStateHide(closingBranchNode, forwards.PrevIsFwd);	} catch (Exception ex) { Debug.LogException(ex); }

			var parent = closingBranchNode.Parent; 
			
			if (!forwards.PrevIsFwd && parent.FirstChild == null && parent.ChildrenShowed)
				parent.State.DoLastChildHide(parent);
				
			closingBranchNode = parent;
		}
		
		var openingBranchNode = commonParent.FirstChild.GetLastSiblingOrNull();
		
		while (openingBranchNode != null)
		{
			if (forwards.NextIsFwd && !openingBranchNode.Parent.ChildrenShowed)
				openingBranchNode.Parent.State.DoFirstChildShow(openingBranchNode.Parent);
		
			try{ NodeStateShow( openingBranchNode, forwards.NextIsFwd );} catch (Exception ex) { Debug.LogException(ex); }
			try{ openingBranchNode.State.gameObject.SetActive( true );	} catch (Exception ex) { Debug.LogException(ex); }
			
			openingBranchNode = openingBranchNode.FirstChild.GetLastSiblingOrNull();
		}
	}
	internal static	Forwards	ComputeForwards			( FlowNode? prev, FlowNode? next )				
	{
		if (prev == null)				return new(true,  true);	// We open new separated state
		if (next == null)				return new(false, false);	// We close last separated state
		if (next.IsInForwardOf(prev))	return new(true,  true);
		if (prev.IsInForwardOf(next))	return new(false, false);
	
		return new(false, true);
	}
	internal static	void		NodeStateHide			( FlowNode node, Boolean isMoveForward )		
	{
		var state = node.State;
		
		try
		{
			if( isMoveForward )	state.DoForwardHide( );
			else				state.DoHide( );
		}
		catch ( Exception ex ) { Debug.LogException( ex ); }
	}
	internal static	void		NodeStateShow			( FlowNode node, Boolean isMoveForward )		
	{
		var state	= node.State;
		state._node	= node;
		
		if (!node.FullyInited && !isMoveForward)
			try						{ state.DoShow(); }
			catch (Exception ex)	{ Debug.LogException(ex); }
		
		node.FullyInited = true;
		
		try
		{
			if (isMoveForward)	state.DoShow();
			else				state.DoBackShow();
		}
		catch (Exception ex) { Debug.LogException(ex); }
	}
	
	internal record struct Forwards(Boolean PrevIsFwd, Boolean NextIsFwd);
}