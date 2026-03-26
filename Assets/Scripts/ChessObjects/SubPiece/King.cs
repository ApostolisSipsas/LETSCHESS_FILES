using System.Collections.Generic;
using UnityEngine;

public class King : Piece
{
    public override List<Vector2Int> GetAvailavleMoves(ref Piece[,] bord, int tcx, int tcy)
    {
        List<Vector2Int> r = new List<Vector2Int>();

        // Κινήσεις αριστερά
        if (posX - 1 >= 0)
        {
            if (bord[posX - 1, posY] == null)
            {
                r.Add(new Vector2Int(posX - 1, posY));
            }
            else if (bord[posX - 1, posY].Color != Color)
            {
                r.Add(new Vector2Int(posX - 1, posY));
            }
            // Πάνω αριστερά
            if (posY + 1 < tcy)
            {
                if (bord[posX - 1, posY + 1] == null)
                {
                    r.Add(new Vector2Int(posX - 1, posY + 1));
                }
                else if (bord[posX - 1, posY + 1].Color != Color)
                {
                    r.Add(new Vector2Int(posX - 1, posY + 1));
                }
            }
            // Κάτω αριστερά
            if (posY - 1 >= 0)
            {
                if (bord[posX - 1, posY - 1] == null)
                {
                    r.Add(new Vector2Int(posX - 1, posY - 1));
                }
                else if (bord[posX - 1, posY - 1].Color != Color)
                {
                    r.Add(new Vector2Int(posX - 1, posY - 1));
                }
            }
        }
        // Κινήσεις δεξιά
        if (posX + 1 < tcx)
        {

            if (bord[posX + 1, posY] == null)
            {
                r.Add(new Vector2Int(posX + 1, posY));
            }
            else if (bord[posX + 1, posY].Color != Color)
            {
                r.Add(new Vector2Int(posX + 1, posY));
            }
            // Πάνω δεξιά
            if (posY + 1 < tcy)
            {
                if (bord[posX + 1, posY + 1] == null)
                {
                    r.Add(new Vector2Int(posX + 1, posY + 1));
                }
                else if (bord[posX + 1, posY + 1].Color != Color)
                {
                    r.Add(new Vector2Int(posX + 1, posY + 1));
                }
            }
            // Κάτω δεξιά
            if (posY - 1 >= 0)
            {
                if (bord[posX + 1, posY - 1] == null)
                {
                    r.Add(new Vector2Int(posX + 1, posY - 1));
                }
                else if (bord[posX + 1, posY - 1].Color != Color)
                {
                    r.Add(new Vector2Int(posX + 1, posY - 1));
                }
            }

        }
        // Κίνηση πάνω
        if (posY + 1 < tcy)
        {
            if (bord[posX,posY+1] == null || bord[posX,posY+1].Color != Color)
            {
                r.Add(new Vector2Int(posX, posY + 1));
            }
        }
        // Κίνηση κάτω
        if (posY -1>=0)
        {
            if (bord[posX, posY - 1] == null || bord[posX, posY -1].Color != Color)
            {
                r.Add(new Vector2Int(posX, posY - 1));
            }
        }
        return r;
        }


    public override SpecialMove GetSpecialMove(ref Piece[,] board, ref List<Vector2Int[]> moveList, ref List<Vector2Int> availableMoves)
    {
        SpecialMove r = SpecialMove.None;
        var kingMove = moveList.Find(m => m[0].x == 4 && m[0].y == ((Color == 0) ? 0 : 7));
        var leftRook = moveList.Find(m => m[0].x == 0 && m[0].y == ((Color == 0) ? 0 : 7));
        var rightRook = moveList.Find(m => m[0].x == 7 && m[0].y == ((Color == 0) ? 0 : 7));

        // Δεν επιτρέπεται castling ενώ ο βασιλιάς είναι σε σαχ
        int bx = board.GetLength(0), by = board.GetLength(1);
        var kingPos = new Vector2Int(posX, posY);
        for (int x = 0; x < bx; x++)
            for (int y = 0; y < by; y++)
                if (board[x, y] != null && board[x, y].Color != Color)
                    if (board[x, y].GetAvailavleMoves(ref board, bx, by).Contains(kingPos))
                        return SpecialMove.None;

        if(kingMove == null && posX == 4)
        {
            // Λευκή ομάδα
            if(Color == 0)
            {
                // Αριστερός πύργος (λευκοί)
                if (leftRook  == null)
                    if (board[0,0].type==PieceType.Rook) 
                        if (board[0,0].Color ==0)
                            if (board[3,0] == null && board[2, 0] == null && board[1, 0] == null)
                            {
                                availableMoves.Add(new Vector2Int(2,0));
                                r = SpecialMove.Castling;
                            }
                // Δεξιός πύργος (λευκοί)
                if (leftRook == null)
                    if (board[7, 0].type == PieceType.Rook)
                        if (board[7, 0].Color == 0)
                            if (board[5, 0] == null && board[6, 0] == null )
                            {
                                availableMoves.Add(new Vector2Int(6, 0));
                                r = SpecialMove.Castling;
                            }
            }
            else
            {
                // Αριστερός πύργος (μαύροι)
                if (leftRook == null)
                    if (board[0, 7].type == PieceType.Rook)
                        if (board[0, 7].Color == 0)
                            if (board[3, 7] == null && board[2, 7] == null && board[1, 7] == null)
                            {
                                availableMoves.Add(new Vector2Int(2, 7));
                                r = SpecialMove.Castling;
                            }
                // Δεξιός πύργος (μαύροι)
                if (leftRook == null)
                    if (board[7, 7].type == PieceType.Rook)
                        if (board[7, 7].Color == 0)
                            if (board[5, 7] == null && board[6, 7] == null)
                            {
                                availableMoves.Add(new Vector2Int(6, 7));
                                r = SpecialMove.Castling;     
                            }
            }
        }
        return r;
    }
}
