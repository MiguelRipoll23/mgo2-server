/* containing function of static patch target(s); entry .opd.FUN_00ab6688 @ 00ab6688 */

void _opd_FUN_00ab6688(void)

{
  bool bVar1;
  ushort uVar2;
  int *piVar3;
  undefined *puVar4;
  undefined2 uVar5;
  int iVar7;
  undefined8 uVar6;
  undefined4 uVar8;
  int iVar9;
  undefined1 auStack_a0 [112];
  
  puVar4 = PTR_PTR_0121bc58;
  piVar3 = *(int **)(PTR_PTR_0121bc58 + -0x8000);
  if (*(short *)(*piVar3 + 0x244e42) == 0x200) {
    iVar7 = _opd_FUN_00eaa9f8();
    if (iVar7 == 0) {
      if (*(short *)(*piVar3 + 0x244e42) != 0x200) goto LAB_00ab66e8;
      goto LAB_00ab6774;
    }
LAB_00ab66f8:
    _opd_FUN_000482b0(0x5e);
    iVar7 = **(int **)(puVar4 + -0x8000);
    iVar9 = iVar7 + 0x240000;
    uVar2 = *(ushort *)(iVar7 + 0x244e42);
    if (uVar2 != 0xc0) {
      if (uVar2 < 0xc1) {
        if (uVar2 == 0x60) goto LAB_00ab67e8;
        if (uVar2 == 0x80) {
LAB_00ab67e0:
          uVar5 = 0xc0;
          goto LAB_00ab6804;
        }
LAB_00ab67f8:
        iVar9 = iVar7 + 0x240000;
      }
      else if (uVar2 != 0x100) {
        bVar1 = uVar2 == 0x200;
LAB_00ab67c0:
        if (bVar1) {
          uVar5 = 0x60;
          goto LAB_00ab6804;
        }
        goto LAB_00ab67f8;
      }
LAB_00ab6800:
      uVar5 = 0x200;
      goto LAB_00ab6804;
    }
  }
  else {
LAB_00ab66e8:
    iVar7 = _opd_FUN_00eac89c();
    if (iVar7 != 0) goto LAB_00ab66f8;
    if (*(short *)(*piVar3 + 0x244e42) == 0x60) {
      iVar7 = _opd_FUN_00eaa8cc();
      if (iVar7 == 0) {
        if (*(short *)(**(int **)(puVar4 + -0x8000) + 0x244e42) == 0x60) {
          return;
        }
        goto LAB_00ab6774;
      }
    }
    else {
LAB_00ab6774:
      iVar7 = _opd_FUN_00eac9f8();
      if (iVar7 == 0) {
        return;
      }
    }
    _opd_FUN_000482b0(0x5e);
    iVar7 = **(int **)(puVar4 + -0x8000);
    iVar9 = iVar7 + 0x240000;
    uVar2 = *(ushort *)(iVar7 + 0x244e42);
    if (uVar2 == 0xc0) {
LAB_00ab67e8:
      uVar5 = 0x80;
      goto LAB_00ab6804;
    }
    if (uVar2 < 0xc1) {
      if (uVar2 != 0x60) {
        bVar1 = uVar2 == 0x80;
        goto LAB_00ab67c0;
      }
      goto LAB_00ab6800;
    }
    if (uVar2 == 0x100) goto LAB_00ab67e0;
    if (uVar2 != 0x200) goto LAB_00ab67f8;
  }
  uVar5 = 0x100;
LAB_00ab6804:
  *(undefined2 *)(iVar9 + 0x4e42) = uVar5;
  uVar6 = 0xa8;
  if (*(short *)(&DAT_00244e3e + iVar7) == 0) {
    uVar6 = 0xa9;
  }
  uVar8 = _opd_FUN_00a0c930(uVar6);
  iVar9 = iVar7 + 0x183450;
  _opd_FUN_00aab5c8(iVar9,0,uVar8);
  uVar8 = *(undefined4 *)(puVar4 + -0x7fc0);
  _opd_FUN_00fa1c08(auStack_a0,uVar8,*(undefined2 *)(_opd_FUN_00244e40 + iVar7));
  _opd_FUN_00aab5c8(iVar9,1,auStack_a0);
  _opd_FUN_000d60b8(auStack_a0,100);
  _opd_FUN_00fa1c08(auStack_a0,uVar8,*(undefined2 *)(iVar7 + 0x244e42));
  uVar8 = _opd_FUN_00a0c930(0xaa);
  _opd_FUN_00f9d840(auStack_a0,uVar8);
  _opd_FUN_00aab5c8(iVar9,2,auStack_a0);
  return;
}

