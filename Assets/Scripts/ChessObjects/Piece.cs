using System.Collections.Generic;
using UnityEngine;

public enum PieceType
{
    None = 0,
    Pawn = 1,
    Rook = 2,
    Knight = 3,
    Bishop = 4,
    Queen = 5,
    King = 6
}

public class Piece : MonoBehaviour
{
    public int Color;      // 0 = Λευκό, 1 = Μαύρο
    public int posX, posY;
    public PieceType type;

    private Vector3 desPosition;
    private Vector3 desScale = Vector3.one;   // στόχος για scale

    private void Start()
    {
        // Rotation για τα πιόνια
        transform.rotation = Quaternion.Euler(-90, 0, (Color == 0) ? -90 : 90);

        // Default scale 1.5 για κανονικό κομμάτι
        transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
        desScale = transform.localScale; // ώστε να μην γίνεται Lerp σε άλλο scale
    }

    private void Update()
    {
        // Lerp στη θέση
        transform.position = Vector3.Lerp(
            transform.position,
            desPosition,
            Time.deltaTime * 10
        );

        // Lerp στο scale (διορθωμένο)
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            desScale,
            Time.deltaTime * 10
        );
    }

    public virtual void setPosition(Vector3 position, bool force = false)
    {
        desPosition = position;

        if (force)
        {
            transform.position = desPosition;
        }
    }

    public virtual void setScale(Vector3 scale, bool force = false)
    {
        desScale = scale;

        if (force)
        {
            transform.localScale = desScale;
        }
    }

    public virtual List<Vector2Int> GetAvailavleMoves(ref Piece[,] board, int tcx, int tcy)
    {
        List<Vector2Int> rTable = new List<Vector2Int>();

        // ΔΕΔΟΜΕΝΑ ΔΟΚΙΜΗΣ - προσωρινά
        rTable.Add(new Vector2Int(3, 3));
        rTable.Add(new Vector2Int(3, 4));
        rTable.Add(new Vector2Int(4, 3));
        rTable.Add(new Vector2Int(4, 4));

        return rTable;
    }

    public virtual SpecialMove GetSpecialMove(
        ref Piece[,] board,
        ref List<Vector2Int[]> moveList,
        ref List<Vector2Int> availableMoves)
    {
        return SpecialMove.None;
    }
}