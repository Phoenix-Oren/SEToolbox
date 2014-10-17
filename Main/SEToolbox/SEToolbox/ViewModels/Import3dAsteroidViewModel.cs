﻿namespace SEToolbox.ViewModels
{
    using System.Linq;
    using System.Windows.Media;
    using Sandbox.Common.ObjectBuilders;
    using Sandbox.Common.ObjectBuilders.Voxels;
    using SEToolbox.Interfaces;
    using SEToolbox.Interop;
    using SEToolbox.Interop.Asteroids;
    using SEToolbox.Models;
    using SEToolbox.Services;
    using SEToolbox.Support;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Windows.Forms;
    using System.Windows.Input;
    using System.Windows.Media.Media3D;
    using VRageMath;
    using Color = VRageMath.Color;
    using Res = SEToolbox.Properties.Resources;

    public class Import3dAsteroidViewModel : BaseViewModel
    {
        #region Fields

        private static readonly object Locker = new object();

        private readonly IDialogService _dialogService;
        private readonly Func<IOpenFileDialog> _openFileDialogFactory;
        private readonly Import3dAsteroidModel _dataModel;

        private bool? _closeResult;
        private bool _isBusy;

        #endregion

        #region Constructors

        public Import3dAsteroidViewModel(BaseViewModel parentViewModel, Import3dAsteroidModel dataModel)
            : this(parentViewModel, dataModel, ServiceLocator.Resolve<IDialogService>(), ServiceLocator.Resolve<IOpenFileDialog>)
        {
        }

        public Import3dAsteroidViewModel(BaseViewModel parentViewModel, Import3dAsteroidModel dataModel, IDialogService dialogService, Func<IOpenFileDialog> openFileDialogFactory)
            : base(parentViewModel)
        {
            Contract.Requires(dialogService != null);
            Contract.Requires(openFileDialogFactory != null);

            this._dialogService = dialogService;
            this._openFileDialogFactory = openFileDialogFactory;
            this._dataModel = dataModel;
            this._dataModel.PropertyChanged += delegate(object sender, PropertyChangedEventArgs e)
            {
                // Will bubble property change events from the Model to the ViewModel.
                this.OnPropertyChanged(e.PropertyName);
            };

            this.IsMultipleScale = true;
            this.MultipleScale = 1;
            this.MaxLengthScale = 100;
        }

        #endregion

        #region command Properties

        public ICommand Browse3dModelCommand
        {
            get
            {
                return new DelegateCommand(new Action(Browse3dModelExecuted), new Func<bool>(Browse3dModelCanExecute));
            }
        }

        public ICommand CreateCommand
        {
            get
            {
                return new DelegateCommand(new Action(CreateExecuted), new Func<bool>(CreateCanExecute));
            }
        }

        public ICommand CancelCommand
        {
            get
            {
                return new DelegateCommand(new Action(CancelExecuted), new Func<bool>(CancelCanExecute));
            }
        }

        #endregion

        #region properties

        /// <summary>
        /// Gets or sets the DialogResult of the View.  If True or False is passed, this initiates the Close().
        /// </summary>
        public bool? CloseResult
        {
            get { return this._closeResult; }

            set
            {
                this._closeResult = value;
                this.RaisePropertyChanged(() => CloseResult);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the View is currently in the middle of an asynchonise operation.
        /// </summary>
        public bool IsBusy
        {
            get { return this._isBusy; }

            set
            {
                if (value != this._isBusy)
                {
                    this._isBusy = value;
                    this.RaisePropertyChanged(() => IsBusy);
                    if (this._isBusy)
                    {
                        System.Windows.Forms.Application.DoEvents();
                    }
                }
            }
        }

        public string Filename
        {
            get { return this._dataModel.Filename; }

            set
            {
                this._dataModel.Filename = value;
                this.FilenameChanged();
            }
        }

        public Model3D Model
        {
            get { return this._dataModel.Model; }
            set { this._dataModel.Model = value; }
        }

        public bool IsValidModel
        {
            get { return this._dataModel.IsValidModel; }
            set { this._dataModel.IsValidModel = value; }
        }

        public BindableSize3DModel OriginalModelSize
        {
            get { return this._dataModel.OriginalModelSize; }
            set { this._dataModel.OriginalModelSize = value; }
        }

        public BindableSize3DIModel NewModelSize
        {
            get { return this._dataModel.NewModelSize; }

            set
            {
                this._dataModel.NewModelSize = value;
                this.ProcessModelScale();
            }
        }

        public BindablePoint3DModel NewModelScale
        {
            get { return this._dataModel.NewModelScale; }
            set { this._dataModel.NewModelScale = value; }
        }

        public BindablePoint3DModel Position
        {
            get { return this._dataModel.Position; }
            set { this._dataModel.Position = value; }
        }

        public BindableVector3DModel Forward
        {
            get { return this._dataModel.Forward; }
            set { this._dataModel.Forward = value; }
        }

        public BindableVector3DModel Up
        {
            get { return this._dataModel.Up; }
            set { this._dataModel.Up = value; }
        }

        public SEToolbox.Interop.Asteroids.MyVoxelRayTracer.TraceType TraceType
        {
            get { return this._dataModel.TraceType; }
            set { this._dataModel.TraceType = value; }
        }

        public double MultipleScale
        {
            get { return this._dataModel.MultipleScale; }
            set { this._dataModel.MultipleScale = value; this.ProcessModelScale(); }
        }

        public double MaxLengthScale
        {
            get { return this._dataModel.MaxLengthScale; }
            set { this._dataModel.MaxLengthScale = value; this.ProcessModelScale(); }
        }

        public double BuildDistance
        {
            get { return this._dataModel.BuildDistance; }

            set
            {
                this._dataModel.BuildDistance = value;
                this.ProcessModelScale();
            }
        }

        public bool IsMultipleScale
        {
            get { return this._dataModel.IsMultipleScale; }

            set
            {
                this._dataModel.IsMultipleScale = value;
                this.ProcessModelScale();
            }
        }

        public bool IsMaxLengthScale
        {
            get { return this._dataModel.IsMaxLengthScale; }

            set
            {
                this._dataModel.IsMaxLengthScale = value;
                this.ProcessModelScale();
            }
        }

        public ObservableCollection<MaterialSelectionModel> OutsideMaterialsCollection
        {
            get { return this._dataModel.OutsideMaterialsCollection; }
        }


        public ObservableCollection<MaterialSelectionModel> InsideMaterialsCollection
        {
            get { return this._dataModel.InsideMaterialsCollection; }
        }

        public MaterialSelectionModel OutsideStockMaterial
        {
            get { return this._dataModel.OutsideStockMaterial; }
            set { this._dataModel.OutsideStockMaterial = value; }
        }

        public MaterialSelectionModel InsideStockMaterial
        {
            get { return this._dataModel.InsideStockMaterial; }
            set { this._dataModel.InsideStockMaterial = value; }
        }

        public string SourceFile
        {
            get { return this._dataModel.SourceFile; }
            set { this._dataModel.SourceFile = value; }
        }

        #endregion

        #region command methods

        public bool Browse3dModelCanExecute()
        {
            return true;
        }

        public void Browse3dModelExecuted()
        {
            this.IsValidModel = false;

            IOpenFileDialog openFileDialog = _openFileDialogFactory();
            openFileDialog.Filter = Res.DialogImportModelFilter;
            openFileDialog.Title = Res.DialogImportModelTitle;

            // Open the dialog
            DialogResult result = _dialogService.ShowOpenFileDialog(this, openFileDialog);

            if (result == DialogResult.OK)
            {
                this.Filename = openFileDialog.FileName;
            }
        }

        private void FilenameChanged()
        {
            this.ProcessFilename(this.Filename);
        }

        public bool CreateCanExecute()
        {
            return this.IsValidModel;
        }

        public void CreateExecuted()
        {
            this.CloseResult = true;
        }

        public bool CancelCanExecute()
        {
            return true;
        }

        public void CancelExecuted()
        {
            this.CloseResult = false;
        }

        #endregion

        #region methods

        private void ProcessFilename(string filename)
        {
            this.IsValidModel = false;
            this.IsBusy = true;

            this.OriginalModelSize = new BindableSize3DModel(0, 0, 0);
            this.NewModelSize = new BindableSize3DIModel(0, 0, 0);
            this.Position = new BindablePoint3DModel(0, 0, 0);

            if (File.Exists(filename))
            {
                // validate file is a real model.
                // read model properties.
                Model3D model;
                var size = Modelling.PreviewModelVolmetic(filename, out model);
                this.Model = model;

                if (size != null && size.Height != 0 && size.Width != 0 && size.Depth != 0)
                {
                    this.OriginalModelSize = size;
                    this.BuildDistance = 10;
                    this.IsValidModel = true;
                    this.ProcessModelScale();
                }
            }

            this.IsBusy = false;
        }

        private void ProcessModelScale()
        {
            if (this.IsValidModel)
            {
                if (this.IsMaxLengthScale)
                {
                    var factor = this.MaxLengthScale / Math.Max(Math.Max(this.OriginalModelSize.Height, this.OriginalModelSize.Width), this.OriginalModelSize.Depth);

                    this.NewModelSize.Height = (int)(factor * this.OriginalModelSize.Height);
                    this.NewModelSize.Width = (int)(factor * this.OriginalModelSize.Width);
                    this.NewModelSize.Depth = (int)(factor * this.OriginalModelSize.Depth);
                }
                else if (this.IsMultipleScale)
                {
                    this.NewModelSize.Height = (int)(this.MultipleScale * this.OriginalModelSize.Height);
                    this.NewModelSize.Width = (int)(this.MultipleScale * this.OriginalModelSize.Width);
                    this.NewModelSize.Depth = (int)(this.MultipleScale * this.OriginalModelSize.Depth);
                }

                double vectorDistance = this.BuildDistance;

                vectorDistance += this.NewModelSize.Depth;
                this.NewModelScale = new BindablePoint3DModel(this.NewModelSize.Width, this.NewModelSize.Height, this.NewModelSize.Depth);

                // Figure out where the Character is facing, and plant the new construct right in front, by "10" units, facing the Character.
                var vector = new BindableVector3DModel(this._dataModel.CharacterPosition.Forward).Vector3D;
                vector.Normalize();
                vector = Vector3D.Multiply(vector, vectorDistance);
                this.Position = new BindablePoint3DModel(Point3D.Add(new BindablePoint3DModel(this._dataModel.CharacterPosition.Position).Point3D, vector));
                this.Forward = new BindableVector3DModel(this._dataModel.CharacterPosition.Forward);
                this.Up = new BindableVector3DModel(this._dataModel.CharacterPosition.Up);
            }
        }

        #endregion

        #region BuildTestEntity

        public MyObjectBuilder_CubeGrid BuildTestEntity()
        {
            var entity = new MyObjectBuilder_CubeGrid
            {
                EntityId = SpaceEngineersApi.GenerateEntityId(),
                PersistentFlags = MyPersistentEntityFlags2.CastShadows | MyPersistentEntityFlags2.InScene,
                Skeleton = new System.Collections.Generic.List<BoneInfo>(),
                LinearVelocity = new VRageMath.Vector3(0, 0, 0),
                AngularVelocity = new VRageMath.Vector3(0, 0, 0),
                GridSizeEnum = MyCubeSize.Large
            };

            var blockPrefix = entity.GridSizeEnum.ToString();
            var cornerBlockPrefix = entity.GridSizeEnum.ToString();

            entity.IsStatic = false;
            blockPrefix += "BlockArmor";        // HeavyBlockArmor|BlockArmor;
            cornerBlockPrefix += "BlockArmor"; // HeavyBlockArmor|BlockArmor|RoundArmor_;

            // Figure out where the Character is facing, and plant the new constrcut right in front, by "10" units, facing the Character.
            var vector = new BindableVector3DModel(this._dataModel.CharacterPosition.Forward).Vector3D;
            vector.Normalize();
            vector = Vector3D.Multiply(vector, 6);
            this.Position = new BindablePoint3DModel(Point3D.Add(new BindablePoint3DModel(this._dataModel.CharacterPosition.Position).Point3D, vector));
            this.Forward = new BindableVector3DModel(this._dataModel.CharacterPosition.Forward);
            this.Up = new BindableVector3DModel(this._dataModel.CharacterPosition.Up);

            entity.PositionAndOrientation = new MyPositionAndOrientation()
            {
                Position = this.Position.ToVector3(),
                Forward = this.Forward.ToVector3(),
                Up = this.Up.ToVector3()
            };

            // Large|BlockArmor|Corner
            // Large|RoundArmor_|Corner
            // Large|HeavyBlockArmor|Block,
            // Small|BlockArmor|Slope,
            // Small|HeavyBlockArmor|Corner,

            var blockType = (SubtypeId)Enum.Parse(typeof(SubtypeId), blockPrefix + "Block");
            var slopeBlockType = (SubtypeId)Enum.Parse(typeof(SubtypeId), cornerBlockPrefix + "Slope");
            var cornerBlockType = (SubtypeId)Enum.Parse(typeof(SubtypeId), cornerBlockPrefix + "Corner");
            var inverseCornerBlockType = (SubtypeId)Enum.Parse(typeof(SubtypeId), cornerBlockPrefix + "CornerInv");

            entity.CubeBlocks = new System.Collections.Generic.List<MyObjectBuilder_CubeBlock>();

            var fixScale = 0;
            if (this.IsMultipleScale && this.MultipleScale == 1)
            {
                fixScale = 0;
            }
            else
            {
                fixScale = Math.Max(Math.Max(this.NewModelSize.Height, this.NewModelSize.Width), this.NewModelSize.Depth);
            }

            //var smoothObject = true;

            // Read in voxel and set main cube space.
            //var ccubic = TestCreateSplayedDiagonalPlane();
            //var ccubic = TestCreateSlopedDiagonalPlane();
            //var ccubic = TestCreateStaggeredStar();
            var ccubic = Modelling.TestCreateTrayShape();
            //var ccubic = ReadModelVolmetic(@"..\..\..\..\..\..\building 3D\models\Rhino_corrected.obj", 10, null, ModelTraceVoxel.ThickSmoothedDown);

            #region Read in voxel and set main cube space.

            #endregion

            //if (smoothObject)
            //{
            //    CalculateAddedInverseCorners(ccubic);
            //    CalculateAddedSlopes(ccubic);
            //    CalculateAddedCorners(ccubic);
            //}

            Modelling.BuildStructureFromCubic(entity, ccubic, blockType, slopeBlockType, cornerBlockType, inverseCornerBlockType);

            return entity;
        }

        #endregion

        #region BuildEntity

        public MyObjectBuilder_VoxelMap BuildEntity()
        {
            var filenamepart = Path.GetFileNameWithoutExtension(this.Filename);
            var filename = this.MainViewModel.CreateUniqueVoxelFilename(filenamepart + ".vox");
            this.Position = this.Position.RoundOff(1.0);
            this.Forward = this.Forward.RoundToAxis();
            this.Up = this.Up.RoundToAxis();

            var entity = new MyObjectBuilder_VoxelMap(this.Position.ToVector3(), filename)
            {
                EntityId = SpaceEngineersApi.GenerateEntityId(),
                PersistentFlags = MyPersistentEntityFlags2.CastShadows | MyPersistentEntityFlags2.InScene,
            };

            double multiplier = 1;
            if (this.IsMultipleScale)
            {
                multiplier = this.MultipleScale;
            }
            else
            {
                multiplier = this.MaxLengthScale / Math.Max(Math.Max(this.OriginalModelSize.Height, this.OriginalModelSize.Width), this.OriginalModelSize.Depth);
            }

            var scale = new ScaleTransform3D(multiplier, multiplier, multiplier);
            var rotateTransform = MeshHelper.TransformVector(new Vector3D(0, 0, 0), 0, 0, 0);
            this.SourceFile = TempfileUtil.NewFilename();

            //var baseMaterial = SpaceEngineersApi.GetMaterialList().FirstOrDefault(m => m.IsRare == false);
            //if (baseMaterial == null)
            //    baseMaterial = SpaceEngineersApi.GetMaterialList().FirstOrDefault();

            //var voxelMap = MyVoxelBuilder.BuildAsteroidFromModel(true, this.Filename, this.SourceFile, this.OutsideStockMaterial.Value, baseMaterial.Name, this.InsideStockMaterial.Value != null, this.InsideStockMaterial.Value, ModelTraceVoxel.ThinSmoothed, multiplier, transform, this.MainViewModel.ResetProgress, this.MainViewModel.IncrementProgress);


            var model = MeshHelper.Load(this.Filename, ignoreErrors: true);

            var meshes = new List<SEToolbox.Interop.Asteroids.MyVoxelRayTracer.MyMeshModel>();
            var geometeries = new List<MeshGeometry3D>();
            foreach (var model3D in model.Children)
            {
                var gm = (GeometryModel3D)model3D;
                var geometry = gm.Geometry as MeshGeometry3D;

                if (geometry != null)
                    geometeries.Add(geometry);
            }
            meshes.Add(new MyVoxelRayTracer.MyMeshModel(geometeries.ToArray(), this.OutsideStockMaterial.Value, this.OutsideStockMaterial.Value));

            var voxelMap = MyVoxelRayTracer.ReadModelAsteroidVolmetic(model, meshes, this.SourceFile, scale, rotateTransform, this.TraceType, this.MainViewModel.ResetProgress, this.MainViewModel.IncrementProgress);
            voxelMap.Save(this.SourceFile);

            this.MainViewModel.ClearProgress();

            entity.PositionAndOrientation = new MyPositionAndOrientation()
            {
                Position = this.Position.ToVector3(),
                Forward = this.Forward.ToVector3(),
                Up = this.Up.ToVector3()
            };

            this.IsValidModel = voxelMap.ContentSize.X > 0 && voxelMap.ContentSize.Y > 0 && voxelMap.ContentSize.Z > 0;

            return entity;
        }

        #endregion
    }
}