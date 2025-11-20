using UnityEngine;
using UnityEngine.Tilemaps;

public class GlobalGrid : Singleton<GlobalGrid>
{
	static Grid _grid;

	[SerializeField] Tilemap terrainTilemap;
	[SerializeField] Tilemap hazardTilemap;

	public Grid Grid { get { return _grid; }}
	public Tilemap TerrainTilemap { get { return terrainTilemap; }}
	public Tilemap HazardTilemap { get { return hazardTilemap; } }

	protected override void Awake ()
	{
		base.Awake();
		_grid = GetComponent<Grid>();
	}

	// Same as Grid.CellToWorld but gets the center instead of bottom left corner
	public static Vector3 CellToWorld(Vector3Int cellPos)
	{
		return _grid.CellToWorld(cellPos) + new Vector3(_grid.cellSize.x, _grid.cellSize.y, 0.0f) / 2.0f;
	}
}
