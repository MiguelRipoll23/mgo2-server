// hook SITE 0x009bd6c8

void _opd_FUN_009bd284(int param_1)

{
  undefined1 uVar1;
  int *piVar2;
  undefined *puVar3;
  int iVar4;
  undefined4 uVar5;
  undefined4 uVar6;
  undefined4 uVar7;
  undefined4 uVar8;
  undefined4 uVar9;
  undefined4 uVar10;
  undefined4 uVar11;
  undefined4 uVar12;
  char cVar15;
  int iVar13;
  uint *puVar14;
  undefined8 uVar16;
  uint uVar17;
  longlong lVar18;
  undefined *puVar19;
  ulonglong uVar20;
  uint uVar21;
  int iVar22;
  undefined4 local_a0 [10];
  
  puVar3 = PTR_PTR_0121bb10;
  iVar4 = _opd_FUN_0097c124();
  puVar19 = &UNK_004c5364 + param_1;
  _opd_FUN_00aa960c(puVar19);
  uVar12 = *(undefined4 *)(param_1 + 0x586dbc);
  uVar5 = _opd_FUN_000cdc90(*(undefined4 *)(puVar3 + -0x7f48));
  uVar6 = _opd_FUN_000cdc90(*(undefined4 *)(puVar3 + -0x7f68));
  uVar7 = _opd_FUN_000cdc90(*(undefined4 *)(puVar3 + -0x7f40));
  uVar8 = _opd_FUN_000cdc90(*(undefined4 *)(puVar3 + -0x7f64));
  uVar9 = _opd_FUN_000cdc90(*(undefined4 *)(puVar3 + -0x7f60));
  uVar10 = _opd_FUN_000cdc90(*(undefined4 *)(puVar3 + -0x7f5c));
  _opd_FUN_00aa9268(puVar19,uVar12,uVar5,uVar6,uVar7,uVar8,uVar9,uVar10);
  uVar12 = *(undefined4 *)(puVar3 + -0x7f04);
  *(undefined1 *)(param_1 + 0x586d4c) = 1;
  uVar5 = *(undefined4 *)(param_1 + 0x586dc0);
  uVar8 = _opd_FUN_000cdc90(uVar12);
  uVar12 = *(undefined4 *)(param_1 + 0x586db8);
  uVar9 = _opd_FUN_000cdc90(*(undefined4 *)(puVar3 + -0x7f00));
  uVar6 = *(undefined4 *)(puVar3 + -0x7f50);
  uVar10 = _opd_FUN_000cdc90(uVar6);
  uVar7 = *(undefined4 *)(puVar3 + -0x7f4c);
  uVar11 = _opd_FUN_000cdc90(uVar7);
  _opd_FUN_00aa8f7c(puVar19,uVar5,uVar12,uVar8,uVar12,uVar9,uVar10,uVar11);
  uVar12 = *(undefined4 *)(param_1 + 0x586dc4);
  uVar8 = _opd_FUN_000cdc90(*(undefined4 *)(puVar3 + -0x7efc));
  uVar5 = *(undefined4 *)(param_1 + 0x586db8);
  uVar9 = _opd_FUN_000cdc90(*(undefined4 *)(puVar3 + -0x7ef8));
  uVar10 = _opd_FUN_000cdc90(uVar6);
  uVar11 = _opd_FUN_000cdc90(uVar7);
  _opd_FUN_00aa8f7c(puVar19,uVar12,uVar5,uVar8,uVar5,uVar9,uVar10,uVar11);
  uVar12 = *(undefined4 *)(param_1 + 0x586dc8);
  uVar8 = _opd_FUN_000cdc90(*(undefined4 *)(puVar3 + -0x7ef4));
  uVar5 = *(undefined4 *)(param_1 + 0x586db8);
  uVar9 = _opd_FUN_000cdc90(*(undefined4 *)(puVar3 + -0x7ef0));
  uVar10 = _opd_FUN_000cdc90(uVar6);
  uVar11 = _opd_FUN_000cdc90(uVar7);
  _opd_FUN_00aa8f7c(puVar19,uVar12,uVar5,uVar8,uVar5,uVar9,uVar10,uVar11);
  uVar12 = *(undefined4 *)(param_1 + 0x586dcc);
  uVar8 = _opd_FUN_000cdc90(*(undefined4 *)(puVar3 + -0x7eec));
  uVar5 = *(undefined4 *)(param_1 + 0x586db8);
  uVar9 = _opd_FUN_000cdc90(*(undefined4 *)(puVar3 + -0x7ee8));
  uVar6 = _opd_FUN_000cdc90(uVar6);
  uVar7 = _opd_FUN_000cdc90(uVar7);
  _opd_FUN_00aa8f7c(puVar19,uVar12,uVar5,uVar8,uVar5,uVar9,uVar6,uVar7);
  piVar2 = *(int **)(puVar3 + -0x8000);
  iVar13 = *piVar2;
  uVar1 = *(undefined1 *)(*(int *)(iVar13 + 0x68) + *(int *)(iVar13 + 0xc1adc) + 0x2f0);
  uVar12 = _opd_FUN_00a0c930(0x1ba);
  uVar5 = _opd_FUN_00a0c930(0x1c2);
  _opd_FUN_00aa8920(puVar19,uVar12,uVar5,*(undefined4 *)(puVar3 + -0x7ee4));
  cVar15 = _opd_FUN_00a0ccb8(uVar1,2);
  if (cVar15 != '\0') {
    uVar12 = _opd_FUN_00a0c930(0x1bb);
    uVar5 = _opd_FUN_00a0c930(0x1c3);
    _opd_FUN_00aa8920(puVar19,uVar12,uVar5,*(undefined4 *)(puVar3 + -0x7ee0));
    *(undefined4 *)(puVar19 + *(int *)(param_1 + 0x586d30) * 0x318 + -8) = 0x2c;
  }
  cVar15 = _opd_FUN_00a0ccb8(uVar1,4);
  if (cVar15 != '\0') {
    cVar15 = _opd_FUN_00f04238(*(undefined4 *)(*piVar2 + 0x60),0);
    if (cVar15 == '\0') {
      uVar12 = _opd_FUN_00a0c930(0x33);
      uVar5 = _opd_FUN_00a0c930(0x33);
      _opd_FUN_00aa8920(puVar19,uVar12,uVar5,0);
    }
    else {
      uVar12 = _opd_FUN_00a0c930(0x1bc);
      uVar5 = _opd_FUN_00a0c930(0x1c4);
      _opd_FUN_00aa8920(puVar19,uVar12,uVar5,*(undefined4 *)(puVar3 + -0x7edc));
      *(undefined4 *)(puVar19 + *(int *)(param_1 + 0x586d30) * 0x318 + -8) = 0x2d;
    }
  }
  cVar15 = *(char *)(*(int *)(param_1 + 0x68) + *(int *)(param_1 + 0xc1adc) + 0x310);
  if (cVar15 == '\x02') {
    uVar20 = 1;
    cVar15 = _opd_FUN_00a0ccb8(uVar1,2);
    if (cVar15 != '\0') goto LAB_009bd77c;
  }
  else if (cVar15 == '\x04') {
    uVar20 = _opd_FUN_00a0ccb8(uVar1,2);
    uVar20 = uVar20 & 0xff;
    cVar15 = _opd_FUN_00a0ccb8(uVar1,4);
    if (cVar15 != '\0') {
      uVar20 = uVar20 + 1;
    }
    goto LAB_009bd77c;
  }
  uVar20 = 0;
LAB_009bd77c:
  _opd_FUN_00aaacc8(puVar19,uVar20);
  _opd_FUN_00aa8804(puVar19);
  _opd_FUN_000c1208(*(undefined8 *)(param_1 + 0x108),0,*(undefined4 *)(param_1 + 0x586d24));
  iVar13 = _opd_FUN_0097c124();
  _opd_FUN_00a0ca3c(*(undefined4 *)(iVar13 + 0x2a4),0x69838a,
                    puVar19 + *(int *)(param_1 + 0x586d28) * 0x318 + 0x181);
  uVar21 = *(int *)(param_1 + 0xc1adc) - *(int *)(param_1 + 0xc1ae0);
  if ((int)uVar21 < 7) {
    uVar21 = uVar21 & (int)~uVar21 >> 0x1f;
  }
  else {
    uVar21 = 6;
  }
  uVar12 = *(undefined4 *)(param_1 + 0x586db8);
  uVar5 = _opd_FUN_000cdc90(*(undefined4 *)(puVar3 + -0x7ffc));
  puVar14 = (uint *)_opd_FUN_00242bf0(uVar12,uVar5);
  if ((puVar14 != (uint *)0x0) && ((*puVar14 >> 0x16 & 1) == 0)) {
    *puVar14 = *puVar14 | 0x40400000;
  }
  uVar12 = _opd_FUN_00ead010(*(undefined4 *)(iVar4 + 0x2a4),local_a0[uVar21]);
  _opd_FUN_00eace98(*(undefined4 *)(param_1 + 0x586db8),*(undefined4 *)(iVar4 + 0x2a4),uVar12);
  _opd_FUN_00246e78(*(undefined4 *)(param_1 + 0x586db8));
  uVar20 = (longlong)(*(int *)(param_1 + 0xc1adc) - *(int *)(param_1 + 0xc1ae0)) - 6;
  lVar18 = ((longlong)uVar20 >> 0x3f & uVar20) + 6;
  if ((int)((uint)lVar18 & ~(uint)(lVar18 >> 0x3f)) < 5) {
    iVar13 = 0xede715;
    if (*(uint *)(param_1 + 0x586d30) != 3) {
      uVar17 = *(uint *)(param_1 + 0x586d30) ^ 2;
      uVar21 = (int)uVar17 >> 0x1f;
      iVar13 = ((int)(uVar21 - (uVar21 ^ uVar17)) >> 0x1f & 0xffd2d462U) + 0xede714;
    }
  }
  else {
    iVar13 = 0xedeb15;
    if (*(uint *)(param_1 + 0x586d30) != 3) {
      uVar17 = *(uint *)(param_1 + 0x586d30) ^ 2;
      uVar21 = (int)uVar17 >> 0x1f;
      iVar13 = ((int)(uVar21 - (uVar21 ^ uVar17)) >> 0x1f & 0xffd2d063U) + 0xedeb14;
    }
  }
  iVar22 = 0;
  iVar4 = 0;
  _opd_FUN_00eacfe8(*(undefined4 *)(param_1 + 0x586db8),iVar13);
  uVar1 = *(undefined1 *)
           (*(int *)(**(int **)(puVar3 + -0x8000) + 0x68) +
           *(int *)(**(int **)(puVar3 + -0x8000) + 0xc1adc) + 0x2f0);
  while( true ) {
    while( true ) {
      uVar16 = 0x307d12;
      if (iVar4 != 0) {
        if (iVar4 == 1) {
          uVar16 = 0x369bbc;
        }
        else {
          uVar16 = 0xd69902;
        }
      }
      uVar12 = _opd_FUN_00ead010(*(undefined4 *)(param_1 + 0x586db8),uVar16);
      if (iVar22 != 1) break;
      uVar5 = *(undefined4 *)(param_1 + 0x586d9c);
      cVar15 = _opd_FUN_00a0ccb8(uVar1,2);
      if (cVar15 == '\0') {
        uVar12 = _opd_FUN_000cdc90(*(undefined4 *)(puVar3 + -0x7ffc));
        puVar14 = (uint *)_opd_FUN_00242bf0(uVar5,uVar12);
        if ((puVar14 != (uint *)0x0) && ((*puVar14 >> 0x16 & 1) != 0)) {
          *puVar14 = *puVar14 & 0xffbfffff | 0x40000000;
        }
      }
      else {
        uVar6 = _opd_FUN_000cdc90(*(undefined4 *)(puVar3 + -0x7ffc));
        puVar14 = (uint *)_opd_FUN_00242bf0(uVar5,uVar6);
        if ((puVar14 != (uint *)0x0) && ((*puVar14 >> 0x16 & 1) == 0)) {
          *puVar14 = *puVar14 | 0x40400000;
        }
        iVar4 = iVar4 + 1;
        uVar6 = _opd_FUN_000cdc90(*(undefined4 *)(puVar3 + -0x7ed8));
        _opd_FUN_00eacfe8(uVar5,uVar6);
        _opd_FUN_00eace98(uVar5,*(undefined4 *)(param_1 + 0x586db8),uVar12);
      }
      iVar22 = 2;
    }
    if (iVar22 == 2) break;
    iVar22 = iVar22 + 1;
    if (2 < iVar22) {
      return;
    }
  }
  uVar5 = *(undefined4 *)(param_1 + 0x586da0);
  cVar15 = _opd_FUN_00a0ccb8(uVar1,4);
  if (cVar15 == '\0') {
    uVar12 = _opd_FUN_000cdc90(*(undefined4 *)(puVar3 + -0x7ffc));
  }
  else {
    cVar15 = _opd_FUN_00f04238(*(undefined4 *)(**(int **)(puVar3 + -0x8000) + 0x60),0);
    if (cVar15 != '\0') {
      uVar6 = _opd_FUN_000cdc90(*(undefined4 *)(puVar3 + -0x7ffc));
      puVar14 = (uint *)_opd_FUN_00242bf0(uVar5,uVar6);
      if ((puVar14 != (uint *)0x0) && ((*puVar14 >> 0x16 & 1) == 0)) {
        *puVar14 = *puVar14 | 0x40400000;
      }
      uVar6 = _opd_FUN_000cdc90(*(undefined4 *)(puVar3 + -0x7ed4));
      _opd_FUN_00eacfe8(uVar5,uVar6);
      _opd_FUN_00eace98(uVar5,*(undefined4 *)(param_1 + 0x586db8),uVar12);
      return;
    }
    uVar12 = _opd_FUN_000cdc90(*(undefined4 *)(puVar3 + -0x7ffc));
  }
  puVar14 = (uint *)_opd_FUN_00242bf0(uVar5,uVar12);
  if (puVar14 == (uint *)0x0) {
    return;
  }
  if ((*puVar14 >> 0x16 & 1) == 0) {
    return;
  }
  *puVar14 = *puVar14 & 0xffbfffff | 0x40000000;
  return;
}

