﻿using System;
using System.Collections;
using System.Collections.Generic;
using MscPartApi.Tools;
using UnityEngine;

namespace MscPartApi.Trigger
{
	internal class Trigger : MonoBehaviour
	{
		private Part part;
		private GameObject parentGameObject;
		private bool disableCollisionWhenInstalled;
		private Rigidbody rigidBody;
		private bool canBeInstalled;
		private Coroutine handleUninstallRoutine;
		private Coroutine verifyInstalledRoutine;
		private Coroutine verifyUninstalledRoutine;

		private IEnumerator HandleUninstall()
		{
			while (part.IsInstalled())
			{
				if (!part.IsFixed() && part.gameObject.IsLookingAt() && UserInteraction.EmptyHand() &&
				    !Tool.HasToolInHand())
				{
					UserInteraction.ShowGuiInteraction(UserInteraction.Type.Disassemble,
						$"Uninstall {part.gameObject.name}");

					if (UserInteraction.RightMouseDown)
					{
						UserInteraction.ShowGuiInteraction(UserInteraction.Type.None);
						part.gameObject.PlayDisassemble();
						Uninstall();
					}
				}

				yield return null;
			}

			handleUninstallRoutine = null;
		}

		private IEnumerator VerifyInstalled()
		{
			var keepVerifying = part.gameObject.transform.parent != parentGameObject.transform
			                    || part.gameObject.transform.localPosition.CompareVector3(part.installPosition)
			                    || part.gameObject.transform.localRotation.eulerAngles.CompareVector3(part.installRotation);

			while (part.IsInstalled() && keepVerifying)
			{
				rigidBody.isKinematic = true;
				part.gameObject.transform.parent = parentGameObject.transform;
				part.gameObject.transform.localPosition = part.installPosition;
				part.gameObject.transform.localRotation = Quaternion.Euler(part.installRotation);
				yield return null;
			}

			verifyInstalledRoutine = null;
		}

		private IEnumerator VerifyUninstalled()
		{
			while (!part.IsInstalled() && part.gameObject.transform.parent == parentGameObject.transform)
			{
				rigidBody.isKinematic = false;
				part.gameObject.transform.parent = null;
				part.gameObject.transform.Translate(Vector3.up * 0.025f);
				yield return null;
			}

			verifyUninstalledRoutine = null;
		}

		internal void Install()
		{
			InvokeActionList(part.preInstallActions);

			part.partSave.installed = true;
			part.gameObject.tag = "Untagged";

			if (handleUninstallRoutine == null)
			{
				handleUninstallRoutine = StartCoroutine(HandleUninstall());
			}

			if (verifyInstalledRoutine == null)
			{
				verifyInstalledRoutine = StartCoroutine(VerifyInstalled());
			}

			if (disableCollisionWhenInstalled)
			{
				part.collider.isTrigger = true;
			}

			part.SetScrewsActive(true);

			canBeInstalled = false;

			InvokeActionList(part.postInstallActions);
		}

		internal void Uninstall()
		{
			InvokeActionList(part.preUninstallActions);

			part.ResetScrews();

			part.childParts.ForEach(delegate(Part part)
			{
				if (part.uninstallWhenParentUninstalls)
				{
					part.Uninstall();
				}
			});

			part.partSave.installed = false;
			part.gameObject.tag = "PART";

			if (!part.IsInstalled() && verifyUninstalledRoutine == null)
			{
				verifyUninstalledRoutine = StartCoroutine(VerifyUninstalled());
			}

			if (disableCollisionWhenInstalled)
			{
				part.collider.isTrigger = false;
			}

			part.SetScrewsActive(false);

			InvokeActionList(part.postUninstallActions);
		}

		private void OnTriggerStay(Collider collider)
		{
			if (!canBeInstalled || !UserInteraction.LeftMouseDown) return;

			UserInteraction.ShowGuiInteraction(UserInteraction.Type.None);
			collider.gameObject.PlayAssemble();
			canBeInstalled = false;
			Install();
		}

		private void OnTriggerEnter(Collider collider)
		{
			if (!(part.uninstallWhenParentUninstalls && part.ParentInstalled()) || !collider.gameObject.IsHolding() ||
			    collider.gameObject != part.gameObject) return;

			UserInteraction.ShowGuiInteraction(UserInteraction.Type.Assemble, $"Install {part.gameObject.name}");
			canBeInstalled = true;
		}

		private void OnTriggerExit(Collider collider)
		{
			if (!canBeInstalled) return;

			canBeInstalled = false;
			UserInteraction.ShowGuiInteraction(UserInteraction.Type.None);
		}

		internal void Init(Part part, GameObject parentGameObject, bool disableCollisionWhenInstalled)
		{
			this.part = part;
			this.parentGameObject = parentGameObject;
			this.disableCollisionWhenInstalled = disableCollisionWhenInstalled;
			rigidBody = part.gameObject.GetComponent<Rigidbody>();
		}

		private void InvokeActionList(List<Action> actions)
		{
			foreach (var action in actions)
			{
				action.Invoke();
			}
		}
	}
}