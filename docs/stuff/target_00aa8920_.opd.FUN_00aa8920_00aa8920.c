// original call TARGET 0x00aa8920

void _opd_FUN_00aa8920(int param_1,int param_2,int param_3,undefined4 param_4)

{
  undefined2 uVar1;
  char *pcVar2;
  undefined *puVar3;
  int iVar4;
  undefined4 uVar5;
  uint *puVar6;
  undefined4 uVar7;
  int iVar8;
  int iVar9;
  longlong lVar10;
  undefined1 local_210 [4];
  undefined1 auStack_20c [4];
  undefined1 local_208;
  undefined1 auStack_207 [463];
  
  puVar3 = PTR_PTR_0121bc38;
  if (999 < *(int *)(param_1 + 0xc19cc) + 1U) {
    return;
  }
  if (param_2 != 0) {
    lVar10 = 0x180;
    iVar8 = 0;
    do {
      pcVar2 = (char *)(iVar8 + param_2);
      iVar4 = *(int *)(param_1 + 0xc19cc) * 0x318 + iVar8;
      iVar8 = iVar8 + 1;
      *(char *)(param_1 + iVar4) = *pcVar2;
      if (*pcVar2 == '\0') break;
      lVar10 = lVar10 + -1;
    } while (lVar10 != 0);
    if (*(char *)(param_1 + *(int *)(param_1 + 0xc19cc) * 0x318) == '\0') {
      uVar1 = **(undefined2 **)(puVar3 + -0x7ffc);
      FUN_00fbc13c(local_210,&local_208,auStack_20c);
      *(undefined1 *)(param_1 + *(int *)(param_1 + 0xc19cc) * 0x318) = local_208;
      *(undefined1 *)(param_1 + *(int *)(param_1 + 0xc19cc) * 0x318 + 1) = 0;
      local_210[0] = (char)((ushort)uVar1 >> 8);
    }
  }
  if (param_3 != 0) {
    iVar8 = 0;
    lVar10 = 0x180;
    do {
      pcVar2 = (char *)(iVar8 + param_3);
      iVar4 = *(int *)(param_1 + 0xc19cc) * 0x318 + iVar8;
      iVar8 = iVar8 + 1;
      *(char *)(param_1 + iVar4 + 0x181) = *pcVar2;
      if (*pcVar2 == '\0') break;
      lVar10 = lVar10 + -1;
    } while (lVar10 != 0);
    if (*(char *)(param_1 + *(int *)(param_1 + 0xc19cc) * 0x318 + 0x181) == '\0') {
      FUN_00fbc13c(&local_208,local_210,auStack_20c);
      *(undefined1 *)(param_1 + *(int *)(param_1 + 0xc19cc) * 0x318 + 0x181) = local_210[0];
      *(undefined1 *)(param_1 + *(int *)(param_1 + 0xc19cc) * 0x318 + 0x182) = 0;
    }
  }
  *(undefined4 *)(param_1 + *(int *)(param_1 + 0xc19cc) * 0x318 + 0x304) = param_4;
  if (*(uint *)(param_1 + 0xc19d0) <= *(uint *)(param_1 + 0xc19cc)) goto LAB_00aa8ef4;
  iVar8 = *(uint *)(param_1 + 0xc19cc) * 0x20 + 0xc15c0;
  if (*(int *)(param_1 + iVar8) != 0) {
    iVar8 = param_1 + iVar8;
    iVar4 = *(int *)(iVar8 + 8);
    if (iVar4 != 0) {
      iVar8 = _opd_FUN_00243840(*(undefined4 *)(iVar8 + 4),iVar4);
      if ((iVar8 == 0) && (*(char *)(param_1 + 0xc19e8) == '\0')) {
        iVar4 = *(int *)(param_1 + 0xc19cc) * 0x20 + 0xc15c0;
        iVar9 = param_1 + iVar4;
        iVar8 = *(int *)(iVar9 + 4);
        if (iVar8 != 0) {
          _opd_FUN_00eace70(iVar8,*(undefined4 *)(iVar9 + 8),*(undefined4 *)(param_1 + iVar4));
        }
      }
      else {
        iVar8 = *(int *)(param_1 + 0xc19cc) * 0x20 + 0xc15c0;
        _opd_FUN_00eacec0(*(undefined4 *)(param_1 + iVar8),*(undefined4 *)(param_1 + iVar8 + 8));
      }
    }
    _opd_FUN_00fa4d48(auStack_207,0,0x1c0);
    _opd_FUN_00fa4d48(&local_208,0,0x1c1);
    iVar4 = *(int *)(param_1 + 0xc19cc) * 0x20;
    iVar8 = *(int *)(param_1 + iVar4 + 0xc15d4);
    if (iVar8 != 0) {
      _opd_FUN_00eacf10(*(undefined4 *)(param_1 + iVar4 + 0xc15c0),iVar8,
                        param_1 + *(int *)(param_1 + 0xc19cc) * 0x318);
    }
    iVar4 = *(int *)(param_1 + 0xc19cc) * 0x20;
    iVar8 = *(int *)(param_1 + iVar4 + 0xc15d8);
    if (iVar8 != 0) {
      _opd_FUN_00eacf10(*(undefined4 *)(param_1 + iVar4 + 0xc15c0),iVar8,
                        param_1 + *(int *)(param_1 + 0xc19cc) * 0x318);
    }
    uVar7 = *(undefined4 *)(param_1 + *(int *)(param_1 + 0xc19cc) * 0x20 + 0xc15c0);
    uVar5 = _opd_FUN_000cdc90(*(undefined4 *)(puVar3 + -0x8000));
    puVar6 = (uint *)_opd_FUN_00ead010(uVar7,uVar5);
    if ((puVar6 != (uint *)0x0) && ((*puVar6 >> 0x16 & 1) == 0)) {
      *puVar6 = *puVar6 | 0x40400000;
    }
  }
  if (*(int *)(param_1 + 0xc19cc) == 0) {
    iVar8 = *(int *)(param_1 + 0xc19c0);
    if (iVar8 != 0) {
      uVar7 = _opd_FUN_000cdc90(*(undefined4 *)(puVar3 + -0x8000));
      puVar6 = (uint *)_opd_FUN_00242bf0(iVar8,uVar7);
      if ((puVar6 != (uint *)0x0) && ((*puVar6 >> 0x16 & 1) == 0)) {
        *puVar6 = *puVar6 | 0x40400000;
      }
      iVar4 = *(int *)(param_1 + 0xc19cc) * 0x20;
      iVar8 = *(int *)(param_1 + iVar4 + 0xc15d0);
      if (iVar8 != 0) {
        _opd_FUN_00eace98(*(undefined4 *)(param_1 + 0xc19c0),
                          *(undefined4 *)(param_1 + iVar4 + 0xc15cc),iVar8);
      }
    }
    if ((*(int *)(_opd_FUN_000c19d8 + param_1) == 0) ||
       (iVar8 = *(int *)(param_1 + *(int *)(param_1 + 0xc19cc) * 0x20 + 0xc15c0), iVar8 == 0))
    goto LAB_00aa8ef4;
    iVar8 = _opd_FUN_00246950(iVar8);
    if (iVar8 != 0) {
      _opd_FUN_00246e78(*(undefined4 *)(param_1 + *(int *)(param_1 + 0xc19cc) * 0x20 + 0xc15c0));
    }
    iVar8 = _opd_FUN_00247cf8(*(undefined4 *)
                               (param_1 + *(int *)(param_1 + 0xc19cc) * 0x20 + 0xc15c0),
                              *(undefined4 *)(_opd_FUN_000c19d8 + param_1));
    if (iVar8 == 0) goto LAB_00aa8ef4;
    _opd_FUN_00246e78(*(undefined4 *)(param_1 + *(int *)(param_1 + 0xc19cc) * 0x20 + 0xc15c0));
    iVar8 = *(int *)(param_1 + 0xc19cc);
    uVar7 = *(undefined4 *)(_opd_FUN_000c19d8 + param_1);
  }
  else {
    if ((*(int *)(param_1 + 0xc19dc) == 0) ||
       (iVar8 = *(int *)(param_1 + *(int *)(param_1 + 0xc19cc) * 0x20 + 0xc15c0), iVar8 == 0))
    goto LAB_00aa8ef4;
    iVar8 = _opd_FUN_00246950(iVar8);
    if (iVar8 != 0) {
      _opd_FUN_00246e78(*(undefined4 *)(param_1 + *(int *)(param_1 + 0xc19cc) * 0x20 + 0xc15c0));
    }
    iVar8 = _opd_FUN_00247cf8(*(undefined4 *)
                               (param_1 + *(int *)(param_1 + 0xc19cc) * 0x20 + 0xc15c0),
                              *(undefined4 *)(param_1 + 0xc19dc));
    if (iVar8 == 0) goto LAB_00aa8ef4;
    _opd_FUN_00246e78(*(undefined4 *)(param_1 + *(int *)(param_1 + 0xc19cc) * 0x20 + 0xc15c0));
    iVar8 = *(int *)(param_1 + 0xc19cc);
    uVar7 = *(undefined4 *)(param_1 + 0xc19dc);
  }
  _opd_FUN_0024ae50(*(undefined4 *)(param_1 + iVar8 * 0x20 + 0xc15c0),uVar7);
LAB_00aa8ef4:
  *(int *)(param_1 + 0xc19cc) = *(int *)(param_1 + 0xc19cc) + 1;
  _opd_FUN_00981294(*(undefined4 *)(param_1 + *(int *)(param_1 + 0xc19c4) * 0x318 + 0x310));
  return;
}

