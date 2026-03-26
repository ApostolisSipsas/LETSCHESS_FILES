using System.Collections.Generic;
using UnityEngine;

public class Rook : Piece
{
    public override List<Vector2Int> GetAvailavleMoves(ref Piece[,] bord, int tcx, int tcy)
    {
        List<Vector2Int> r = new List<Vector2Int>();
        // Κινήσεις προς τα κάτω
        for (int i = posY-1; i>= 0; i--)
        {
            if (bord[posX,i]== null)
            {
                r.Add(new Vector2Int(posX, i));
            }
            if (bord[posX, i] != null)
            {   
                if(bord[posX,i].Color != Color)
                    r.Add(new Vector2Int(posX, i));
                break;
            }
        }
        // Κινήσεις προς τα πάνω
        for (int i = posY +1; i<tcy; i++)
        {
            if (bord[posX, i] == null)
            {
                r.Add(new Vector2Int(posX, i));
            }
            if (bord[posX, i] != null)
            {
                if (bord[posX, i].Color != Color)
                    r.Add(new Vector2Int(posX, i));
                break;
            }
        }
        // Κινήσεις προς τα αριστερά
        for (int i = posX - 1; i >= 0; i--)
        {
            if (bord[i, posY] == null)
            {
                r.Add(new Vector2Int(i, posY));
            }
            if (bord[i, posY] != null)
            {
                if (bord[i, posY].Color != Color)
                    r.Add(new Vector2Int(i, posY));
                break;
            }
        }
        // Κινήσεις προς τα δεξιά
        for (int i = posX + 1; i < tcx; i++)
        {
            if (bord[i, posY] == null)
            {
                r.Add(new Vector2Int(i,posY));
            }
            if (bord[i, posY] != null)
            {
                if (bord[i, posY].Color != Color)
                    r.Add(new Vector2Int(i, posY));
                break;
            }
        }
        return r;
    }
}
