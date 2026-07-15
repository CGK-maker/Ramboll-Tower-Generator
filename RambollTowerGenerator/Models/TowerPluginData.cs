using Tekla.Structures.Plugins;

namespace RambollTowerGenerator.Models
{
    public class TowerPluginData
    {
        [StructuresField("towertype")]
        public int TowerType;

        [StructuresField("profile")]
        public string profile;

        [StructuresField("material")]
        public string material;

        [StructuresField("basewidth")]
        public double basewidth;

        [StructuresField("topwidth")]
        public double topwidth ;

        [StructuresField("segmentcount")]
        public int segmentcount;

        [StructuresField("totalheight")]
        public double totalheight;

        [StructuresField("cagetype")]
        public int cagetype;

        [StructuresField("verticalheight")]
        public double verticalheight;

        [StructuresField("vertsegcnt")]
        public int verticalsegmentcount;

        [StructuresField("bracingtype")]
        public int bracingtype;

        [StructuresField("diagprofile")]
        public string diagprofile;

        [StructuresField("diagmaterial")]
        public string diagmaterial;

        [StructuresField("horizprofile")]
        public string horizprofile;

        [StructuresField("horizmaterial")]
        public string horizmaterial;

        [StructuresField("bracinglevels")]
        public int bracinglevels;

        [StructuresField("topoffset")]
        public double topoffset;

        [StructuresField("bottomoffset")]
        public double bottomoffset;
    }
}
