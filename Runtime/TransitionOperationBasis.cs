namespace Flexy.GameFlow;

internal static class TransitionOperationBasis
{
	internal static	void		InstantTransition		( FlowNode prevNode, FlowNode nextNode )		
	{
		if (prevNode == nextNode)
			return;
	
		var isMoveForward	= IsForward(prevNode, nextNode);
		var commonParent	= FindNearestCommonParent( prevNode, nextNode );

		var closingBranchNode = prevNode;
		while (closingBranchNode != commonParent)
		{
			try{ closingBranchNode.State.gameObject.SetActive(false);	} catch (Exception ex) { Debug.LogException(ex); }
			try{ NodeStateHide(closingBranchNode, isMoveForward);		} catch (Exception ex) { Debug.LogException(ex); }

			closingBranchNode = closingBranchNode.Parent;
			
			if (!isMoveForward && closingBranchNode.FirstChild == null)
				closingBranchNode.State.DoLastChildHide();
		}
		
		var openingBranchNode = commonParent.FirstChild.GetLastSiblingOrNull();
		
		if (openingBranchNode == null)
			nextNode.Graph._mainLineActive = commonParent;
		
		while (openingBranchNode != null)
		{
			nextNode.Graph._mainLineActive = openingBranchNode;
		
			if (isMoveForward && openingBranchNode.Parent.FirstChild!.NextSibling == null)
				openingBranchNode.Parent.State.DoFirstChildShow();
		
			try{ NodeStateShow( openingBranchNode, isMoveForward );		} catch (Exception ex) { Debug.LogException(ex); }
			try{ openingBranchNode.State.gameObject.SetActive( true );	} catch (Exception ex) { Debug.LogException(ex); }
			
			openingBranchNode = openingBranchNode.FirstChild.GetLastSiblingOrNull();
		}
	}
	
	internal static	Boolean		IsForward				( FlowNode? prev, FlowNode? next )						
	{
		if (prev == null) // We open new separated state
			return true;
			
		if (next == null) // We close last separated state
			return false;
	
		for (var iter = next.Back; iter != null; iter = iter.Back)
		{
			if (iter == prev)
				return true;
		}
		
		return false;
	}
	internal static	FlowNode	FindNearestCommonParent	( FlowNode? nodeA, FlowNode? nodeB )					
	{
		var aSet = new HashSet<FlowNode>();

		for ( ; nodeA != null; nodeA = nodeA.Parent)
			aSet.Add( nodeA );

		for ( ; nodeB != null; nodeB = nodeB.Parent)
			if (aSet.Contains( nodeB ))
				return nodeB;

		throw new InvalidOperationException("Graph broken, can not find common parent, it must be at least one common parent -> Root of the graph ");
	}
	internal static	void		NodeStateHide			( FlowNode node, Boolean isMoveForward )				
	{
		var state = node.State;
		
		try
		{
			if( isMoveForward )	state.DoFwdHide( );
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
}