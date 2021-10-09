﻿using System;
using System.Collections.Generic;
using System.Linq;
using MSCLoader;
using MscPartApi.Tools;
using MscPartApi.Trigger;
using UnityEngine;

namespace MscPartApi
{
	public class Part
	{
		private int clampsAdded = 0;
		private bool partFixed = false;

		internal List<Part> childParts = new List<Part>();
		private PartBaseInfo partBaseInfo;
		internal GameObject gameObject;
		internal PartSave partSave;
		internal Vector3 installPosition;
		internal bool uninstallWhenParentUninstalls;
		internal Vector3 installRotation;
		private GameObject parentGameObject;
		private Part parentPart;
		private List<Screw> savedScrews;
		internal Collider collider;
		public TriggerWrapper trigger;

		private bool usingPartParent = false;
		
		internal List<Action> preInstallActions = new List<Action>();
		internal List<Action> postInstallActions = new List<Action>();

		internal List<Action> preUninstallActions = new List<Action>();
		internal List<Action> postUninstallActions = new List<Action>();
		

		private void Setup(string prefabName, string name, GameObject parentGameObject, Vector3 installPosition, Vector3 installRotation, PartBaseInfo partBaseInfo, bool uninstallWhenParentUninstalls, bool disableCollisionWhenInstalled = true)
		{
			this.partBaseInfo = partBaseInfo;
			this.installPosition = installPosition;
			this.uninstallWhenParentUninstalls = uninstallWhenParentUninstalls;
			this.installRotation = installRotation;
			this.parentGameObject = parentGameObject;

			gameObject = Helper.LoadPartAndSetName(partBaseInfo.assetBundle, prefabName, name);
			if (!partBaseInfo.partsSave.TryGetValue(name, out this.partSave)) {
				partSave = new PartSave();
			}

			this.savedScrews = new List<Screw>(partSave.screws);
			partSave.screws.Clear();

			collider = gameObject.GetComponent<Collider>();

			trigger = new TriggerWrapper(this, parentGameObject, disableCollisionWhenInstalled);

			if (partSave.installed) {
				Install();
			}
		}

		public Part(string prefabName, string name, GameObject parent, Vector3 installPosition, Vector3 installRotation, PartBaseInfo partBaseInfo, bool uninstallWhenParentUninstalls = true, bool disableCollisionWhenInstalled = true)
		{
			Setup(prefabName, name, parent, installPosition, installRotation, partBaseInfo, uninstallWhenParentUninstalls, disableCollisionWhenInstalled);
		}

		public Part(string prefabName, string name, Part parentPart, Vector3 installPosition, Vector3 installRotation, PartBaseInfo partBaseInfo, bool uninstallWhenParentUninstalls = true, bool disableCollisionWhenInstalled = true)
		{
			usingPartParent = true;
			this.parentPart = parentPart;
			Setup(prefabName, name, parentPart.gameObject, installPosition, installRotation, partBaseInfo, disableCollisionWhenInstalled);
			parentPart.childParts.Add(this);
		}

		public void SetPosition(Vector3 position)
		{
			gameObject.transform.position = position;
		}

		internal void ResetScrews()
		{
			foreach (var screw in partSave.screws)
			{
				screw.OutBy(screw.tightness);
			}
		}

		public List<Screw> GetScrews()
		{
			return partSave.screws;
		}

		internal void SetScrewsActive(bool active)
		{
			partSave.screws.ForEach(delegate(Screw screw)
			{
				screw.gameObject.SetActive(active);
			});
		}

		public void SetRotation(Vector3 rotation)
		{
			gameObject.transform.rotation = Quaternion.Euler(rotation);
		}

		public void Install() => trigger.Install();

		public bool IsInstalled()
		{
			return partSave.installed;
		}

		public bool IsFixed()
		{
			return partFixed;
		}

		public void SetFixed(bool partFixed) => this.partFixed = partFixed;

		public void Uninstall()
		{
			trigger.Uninstall();
		}
		/// <summary>
		/// This function adds a simple clamp model to the parent calling it {parent name}_CLAMP{clampIndex}
		/// </summary>
		/// <param name="position">The position on the parent to place this clamp</param>
		/// <param name="rotation">The rotation on the parent to place this clamp</param>
		/// <param name="scale">The scale of the model of the clamp</param>
		public void AddClampModel(Vector3 position, Vector3 rotation, Vector3 scale)
		{
			var clamp = GameObject.Instantiate(MscPartApi.clampModel);
			clamp.name = $"{gameObject.name}_clamp_{clampsAdded}";
			clampsAdded++;
			clamp.transform.SetParent(gameObject.transform);
			clamp.transform.localPosition = position;
			clamp.transform.localScale = scale;
			clamp.transform.localRotation = new Quaternion { eulerAngles = rotation };
		}

		internal bool ParentInstalled()
		{
			if (usingPartParent) {
				return parentPart.IsInstalled();
			} else {
				//Todo: Implement normal msc parts installed/uninstalled
				return true;
			}
		}

		public void AddScrew(Screw screw)
		{
			screw.Verify();
			screw.SetPart(this);
			screw.parentCollider = gameObject.GetComponent<Collider>();
			partSave.screws.Add(screw);

			var index = partSave.screws.IndexOf(screw);

			screw.LoadTightness(savedScrews.ElementAtOrDefault(index));

			screw.CreateScrewModel(index);

			screw.InBy(screw.tightness);

			screw.gameObject.SetActive(IsInstalled());

			MscPartApi.screws.Add(screw.gameObject.name, screw);
		}

		public void AddScrews(Screw[] screws, float overrideScale = 0f, float overrideSize = 0f)
		{
			foreach (var screw in screws)
			{
				if (overrideScale != 0f)
				{
					screw.scale = overrideScale;
				}

				if (overrideSize != 0f)
				{
					screw.size = overrideSize;
				}
				AddScrew(screw);
			}
		}

		public void AddPreInstallAction(Action action)
		{
			preInstallActions.Add(action);
		}

		public void AddPostInstallAction(Action action)
		{
			postInstallActions.Add(action);
		}

		public void AddPreUninstallAction(Action action)
		{
			preUninstallActions.Add(action);
		}

		public void AddPostUninstallAction(Action action)
		{
			postUninstallActions.Add(action);
		}
	}
}