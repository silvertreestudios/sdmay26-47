using System.Collections.Generic;
using UnityEngine;

namespace TacticsGame.Grid
{
    public class GridNode
    {
        public Vector3Int Coordinates;
        public TerrainDef Terrain;
        public List<IGridEntity> Entities = new();

        public GridNode(Vector3Int coordinates, TerrainDef terrain)
        {
            Coordinates = coordinates;
            Terrain = terrain;
        }

        public CoverType GetCoverType()
        {
            CoverType cover = Terrain != null ? Terrain.CoverType : CoverType.None;

            foreach (IGridEntity entity in Entities)
            {
                if (entity != null && entity.CoverType > cover)
                    cover = entity.CoverType;
            }

            return cover;
        }

        public bool BlocksLineOfSight()
        {
            return GetCoverType() == CoverType.Total;
        }

        public bool IsWalkable()
        {
            if (Terrain == null || !Terrain.IsWalkable)
                return false;

            foreach (IGridEntity entity in Entities)
            {
                if (entity != null && entity.BlocksMovement)
                    return false;
            }

            return true;
        }
    }
}
