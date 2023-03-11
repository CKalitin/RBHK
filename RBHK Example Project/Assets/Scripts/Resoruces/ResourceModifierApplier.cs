using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResourceModifierApplier : MonoBehaviour {
    [Header("Resource Modidifer Applier")]
    [SerializeField] private ResourceModifier[] resourceModifiers;
    [SerializeField] private int effectRadius;

    private List<int> appliedResourceModifiers = new List<int>();

    [Header("Other")]
    [SerializeField] Tile tile;

    public void ApplyResourceModifiers() {
        if (TileManagement.instance.SpawningComplete) {
            // Get all adjacent tiles 
            List<Vector2Int> locs = TileManagement.instance.GetAdjacentTilesInRadius(tile.TileInfo.Location, effectRadius);
            
            // Loop through adjacent tiles
            for (int i = 0; i < locs.Count; i++) {
                Tile targetTile = TileManagement.instance.GetTileAtLocation(locs[i]).Tile;

                for (int x = 0; x < resourceModifiers.Length; x++) {
                    appliedResourceModifiers.Add(targetTile.ResourceModifiers.Add(resourceModifiers[x]));
                }
                targetTile.UpdateResourceModifiers();
            }
        }
    }

    public void RemoveResourceModifiers() {
        if (TileManagement.instance.SpawningComplete) {
            // Get all adjacent tiles 
            List<Vector2Int> locs = TileManagement.instance.GetAdjacentTilesInRadius(tile.TileInfo.Location, effectRadius);

            // Loop through adjacent tiles
            for (int i = 0; i < locs.Count; i++) {
                Tile targetTile = TileManagement.instance.GetTileAtLocation(locs[i]).Tile;

                for (int x = 0; x < appliedResourceModifiers.Count; x++) {
                    targetTile.ResourceModifiers.Remove(appliedResourceModifiers[x]);
                }
                targetTile.UpdateResourceModifiers();
            }
        }
    }

    private void OnDestroy() {
        RemoveResourceModifiers();
    }
}
