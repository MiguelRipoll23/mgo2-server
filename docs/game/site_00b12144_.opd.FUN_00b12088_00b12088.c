// hook SITE 0x00b12144

longlong _opd_FUN_00b12088(void)

{
  bool bVar1;
  float fVar2;
  uint uVar3;
  uint uVar4;
  ulonglong *puVar5;
  undefined *puVar6;
  uint uVar7;
  ulonglong uVar8;
  int iVar11;
  longlong lVar9;
  int iVar12;
  undefined4 uVar13;
  undefined4 uVar14;
  undefined4 uVar15;
  undefined4 uVar16;
  ulonglong uVar10;
  undefined *puVar17;
  int iVar18;
  undefined2 uVar19;
  undefined2 uVar20;
  longlong lVar21;
  ulonglong unaff_r14;
  ulonglong unaff_r15;
  ulonglong unaff_r16;
  ulonglong unaff_r17;
  ulonglong unaff_r18;
  int iVar22;
  ulonglong unaff_r19;
  undefined4 *puVar23;
  ulonglong unaff_r20;
  undefined4 *puVar24;
  ulonglong unaff_r21;
  longlong lVar25;
  ulonglong unaff_r22;
  int iVar26;
  ulonglong unaff_r23;
  int iVar27;
  ulonglong unaff_r24;
  ulonglong unaff_r25;
  ulonglong unaff_r26;
  ulonglong unaff_r27;
  ulonglong unaff_r28;
  undefined4 *puVar28;
  ulonglong unaff_r29;
  ulonglong unaff_r30;
  ulonglong unaff_r31;
  undefined8 uVar29;
  ulonglong in_LR;
  ulonglong uVar30;
  ulonglong uVar31;
  ulonglong in_f29;
  double dVar32;
  ulonglong in_f30;
  double dVar33;
  double dVar34;
  ulonglong in_f31;
  undefined4 uVar36;
  undefined4 uVar37;
  undefined1 auVar35 [16];
  undefined4 uVar38;
  undefined4 uVar39;
  undefined1 auVar40 [16];
  undefined1 in_vs37 [16];
  undefined1 auVar41 [16];
  undefined1 auVar42 [16];
  undefined1 auVar43 [16];
  undefined1 auVar44 [16];
  undefined4 uVar45;
  undefined4 uVar46;
  undefined4 uVar47;
  undefined4 uVar48;
  undefined4 uVar49;
  undefined4 uVar54;
  undefined4 uVar55;
  undefined4 uVar56;
  undefined1 auVar50 [16];
  undefined1 auVar51 [16];
  undefined1 auVar52 [16];
  undefined1 auVar53 [16];
  undefined1 auVar57 [16];
  undefined1 auVar58 [16];
  undefined1 auVar59 [16];
  undefined1 auVar60 [16];
  undefined1 auVar61 [16];
  undefined4 in_vr28_32_0;
  undefined4 in_vr28_32_1;
  undefined4 in_vr28_32_2;
  undefined4 in_vr28_32_3;
  undefined1 auVar62 [16];
  undefined4 in_vr29_32_0;
  undefined4 in_vr29_32_1;
  undefined4 in_vr29_32_2;
  undefined4 in_vr29_32_3;
  undefined4 in_vr30_32_0;
  undefined4 in_vr30_32_1;
  undefined4 in_vr30_32_2;
  undefined4 in_vr30_32_3;
  undefined4 in_vr31_32_0;
  undefined4 in_vr31_32_1;
  undefined4 in_vr31_32_2;
  undefined4 in_vr31_32_3;
  undefined4 local_230 [140];
  
  uVar8 = ZEXT48(&stack0x00000000);
  puVar5 = (ulonglong *)(uVar8 - 0x300);
  *puVar5 = uVar8;
  *(undefined4 *)(puVar5 + 0x42) = in_vr28_32_0;
  *(undefined4 *)((int)puVar5 + 0x214) = in_vr28_32_1;
  *(undefined4 *)(puVar5 + 0x43) = in_vr28_32_2;
  *(undefined4 *)((int)puVar5 + 0x21c) = in_vr28_32_3;
  *(undefined4 *)(puVar5 + 0x44) = in_vr29_32_0;
  *(undefined4 *)((int)puVar5 + 0x224) = in_vr29_32_1;
  *(undefined4 *)(puVar5 + 0x45) = in_vr29_32_2;
  *(undefined4 *)((int)puVar5 + 0x22c) = in_vr29_32_3;
  *(undefined4 *)(puVar5 + 0x46) = in_vr30_32_0;
  *(undefined4 *)((int)puVar5 + 0x234) = in_vr30_32_1;
  *(undefined4 *)(puVar5 + 0x47) = in_vr30_32_2;
  *(undefined4 *)((int)puVar5 + 0x23c) = in_vr30_32_3;
  *(undefined4 *)(puVar5 + 0x48) = in_vr31_32_0;
  *(undefined4 *)((int)puVar5 + 0x244) = in_vr31_32_1;
  *(undefined4 *)(puVar5 + 0x49) = in_vr31_32_2;
  *(undefined4 *)((int)puVar5 + 0x24c) = in_vr31_32_3;
  puVar5[0x5b] = unaff_r30;
  puVar6 = PTR_PTR_0121bd10;
  puVar5[0x5c] = unaff_r31;
  puVar5[0x4b] = unaff_r14;
  puVar5[0x5a] = unaff_r29;
  puVar5[0x5d] = in_f29;
  uVar29 = *(undefined8 *)(puVar6 + -0x7b90);
  puVar5[0x5e] = in_f30;
  puVar5[0x5f] = in_f31;
  puVar5[0x4c] = unaff_r15;
  puVar5[0x4d] = unaff_r16;
  puVar5[0x4e] = unaff_r17;
  puVar5[0x4f] = unaff_r18;
  puVar5[0x50] = unaff_r19;
  puVar5[0x51] = unaff_r20;
  puVar5[0x52] = unaff_r21;
  puVar5[0x53] = unaff_r22;
  puVar5[0x54] = unaff_r23;
  puVar5[0x55] = unaff_r24;
  puVar5[0x56] = unaff_r25;
  puVar5[0x57] = unaff_r26;
  puVar5[0x58] = unaff_r27;
  puVar5[0x59] = unaff_r28;
  puVar5[0x62] = in_LR;
  iVar11 = _opd_FUN_000be778(0x1140,uVar29);
  if (iVar11 == 0) {
    return 0;
  }
  lVar9 = _opd_FUN_000bead8(iVar11,uVar29,*(undefined4 *)(puVar6 + -0x7b88),
                            *(undefined4 *)(puVar6 + -0x7b84));
  if (lVar9 == 0) {
LAB_00b12158:
    _opd_FUN_000c1e00(iVar11);
    return 0;
  }
  iVar12 = _opd_FUN_00242398(0xb50127,2,2,0xc2,1,0,0);
  *(int *)(iVar11 + 0x60) = iVar12;
  if (iVar12 == 0) goto LAB_00b12158;
  _opd_FUN_0024d078((double)*(float *)(puVar6 + -0x7f90),iVar12);
  iVar12 = _opd_FUN_00b1e138();
  if (iVar12 != 0) {
    iVar12 = _opd_FUN_00242398(0x2ea916,2,2,0xc2,1,0,0);
    *(int *)(iVar11 + 0x68) = iVar12;
    if (iVar12 == 0) goto LAB_00b12158;
    uVar13 = _opd_FUN_00242bf0(*(undefined4 *)(iVar11 + 0x60),0x361a4e);
    _opd_FUN_00243718(iVar12,uVar13);
    _opd_FUN_0024d078((double)*(float *)(puVar6 + -0x7f90),*(undefined4 *)(iVar11 + 0x68));
  }
  iVar12 = _opd_FUN_00b18ab0();
  if ((((iVar12 != 0) || (iVar12 = _opd_FUN_00b18a70(), iVar12 != 0)) ||
      (iVar12 = _opd_FUN_00b189b0(), iVar12 != 0)) ||
     (((iVar12 = _opd_FUN_00b189f0(), iVar12 != 0 || (iVar12 = _opd_FUN_00b18970(), iVar12 != 0)) ||
      ((iVar12 = _opd_FUN_00b187f0(), iVar12 != 0 ||
       ((iVar12 = _opd_FUN_00b18730(), iVar12 != 0 || (iVar12 = _opd_FUN_00b18870(), iVar12 != 0))))
      )))) {
    *(byte *)(iVar11 + 100) = *(byte *)(iVar11 + 100) | 1;
  }
  iVar12 = _opd_FUN_00b1e138();
  if (((iVar12 != 0) || (iVar12 = _opd_FUN_00b18a30(), iVar12 != 0)) ||
     (iVar12 = _opd_FUN_00b18830(), iVar12 != 0)) {
    iVar12 = _opd_FUN_00242398(0x399138,2,2,0xc2,1,0,0);
    *(int *)(iVar11 + 0x6c) = iVar12;
    if (iVar12 == 0) goto LAB_00b12158;
    uVar13 = _opd_FUN_00242bf0(*(undefined4 *)(iVar11 + 0x60),0x361a4e);
    _opd_FUN_00243718(iVar12,uVar13);
    _opd_FUN_0024d078((double)*(float *)(puVar6 + -0x7f90),*(undefined4 *)(iVar11 + 0x6c));
  }
  _opd_FUN_00b08b30(iVar11);
  iVar12 = *(int *)(puVar6 + -0x7ffc);
  iVar26 = 0;
  uVar13 = *(undefined4 *)(iVar12 + 0x388);
  uVar16 = *(undefined4 *)(iVar12 + 0x38c);
  uVar36 = *(undefined4 *)(iVar12 + 0x390);
  uVar37 = *(undefined4 *)(iVar12 + 0x394);
  uVar15 = *(undefined4 *)(iVar12 + 0x398);
  uVar14 = *(undefined4 *)(iVar12 + 0x39c);
  uVar38 = *(undefined4 *)(iVar12 + 0x3a0);
  uVar39 = *(undefined4 *)(iVar12 + 0x3a4);
  uVar45 = *(undefined4 *)(iVar12 + 0x3a8);
  uVar46 = *(undefined4 *)(iVar12 + 0x3ac);
  *(undefined4 *)(puVar5 + 0x1e) = *(undefined4 *)(iVar12 + 900);
  *(undefined4 *)((int)puVar5 + 0xf4) = uVar13;
  *(undefined4 *)(puVar5 + 0x1f) = uVar16;
  *(undefined4 *)((int)puVar5 + 0xfc) = uVar36;
  *(undefined4 *)(puVar5 + 0x20) = uVar37;
  *(undefined4 *)((int)puVar5 + 0x104) = uVar15;
  *(undefined4 *)(puVar5 + 0x16) = uVar14;
  *(undefined4 *)((int)puVar5 + 0xb4) = uVar38;
  *(undefined4 *)(puVar5 + 0x17) = uVar39;
  *(undefined4 *)((int)puVar5 + 0xbc) = uVar45;
  *(undefined4 *)(puVar5 + 0x18) = uVar46;
  iVar22 = iVar11 + 0xc0;
  do {
    uVar13 = *(undefined4 *)(puVar5 + 0x1e);
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar13,*(undefined4 *)(iVar22 + 4));
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar13,*(undefined4 *)(iVar22 + 0xc));
    uVar16 = *(undefined4 *)((int)puVar5 + 0xf4);
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar16,*(undefined4 *)(iVar22 + 4));
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar16,*(undefined4 *)(iVar22 + 0xc));
    uVar36 = *(undefined4 *)(puVar5 + 0x1f);
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar36,*(undefined4 *)(iVar22 + 4));
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar36,*(undefined4 *)(iVar22 + 0xc));
    uVar37 = *(undefined4 *)((int)puVar5 + 0xfc);
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar37,*(undefined4 *)(iVar22 + 4));
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar37,*(undefined4 *)(iVar22 + 0xc));
    uVar15 = *(undefined4 *)(puVar5 + 0x20);
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar15,*(undefined4 *)(iVar22 + 4));
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar15,*(undefined4 *)(iVar22 + 0xc));
    uVar15 = *(undefined4 *)((int)puVar5 + 0x104);
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar15,*(undefined4 *)(iVar22 + 4));
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar15,*(undefined4 *)(iVar22 + 0xc));
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),0xa42361,*(undefined4 *)(iVar22 + 4));
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),0x580ed9,*(undefined4 *)(iVar22 + 4));
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),0xc74d1e,*(undefined4 *)(iVar22 + 8));
    bVar1 = iVar26 != 0xf;
    iVar26 = iVar26 + 1;
    iVar22 = iVar22 + 0x38;
  } while (bVar1);
  iVar22 = 0;
  _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),0xa42361,*(undefined4 *)(iVar11 + 0x8c));
  _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),0x580ed9,*(undefined4 *)(iVar11 + 0x8c));
  _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),0xc74d1e,*(undefined4 *)(iVar11 + 0x90));
  puVar28 = (undefined4 *)(iVar11 + 0xf48);
  puVar24 = (undefined4 *)(iVar11 + 0xf34);
  puVar23 = (undefined4 *)(iVar11 + 0x620);
  do {
    while (_opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar13,*puVar23), 4 < iVar22) {
      puVar24 = puVar24 + 1;
      puVar28 = puVar28 + 1;
      _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar16,*puVar23);
      _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar36,*puVar23);
      _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar37,*puVar23);
      bVar1 = iVar22 == 6;
      iVar22 = iVar22 + 1;
      puVar23 = puVar23 + 5;
      if (bVar1) goto LAB_00b12770;
    }
    uVar15 = *(undefined4 *)(iVar11 + 0x60);
    uVar14 = _opd_FUN_00242bf0(uVar15,*puVar24);
    _opd_FUN_002495a8(uVar15,uVar13,uVar14);
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar13,*puVar28);
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar16,*puVar23);
    uVar15 = *(undefined4 *)(iVar11 + 0x60);
    uVar14 = _opd_FUN_00242bf0(uVar15,*puVar24);
    _opd_FUN_002495a8(uVar15,uVar16,uVar14);
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar16,*puVar28);
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar36,*puVar23);
    uVar15 = *(undefined4 *)(iVar11 + 0x60);
    uVar14 = _opd_FUN_00242bf0(uVar15,*puVar24);
    _opd_FUN_002495a8(uVar15,uVar36,uVar14);
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar36,*puVar28);
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar37,*puVar23);
    uVar15 = *(undefined4 *)(iVar11 + 0x60);
    uVar14 = _opd_FUN_00242bf0(uVar15,*puVar24);
    _opd_FUN_002495a8(uVar15,uVar37,uVar14);
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar37,*puVar28);
    bVar1 = iVar22 != 6;
    iVar22 = iVar22 + 1;
    puVar28 = puVar28 + 1;
    puVar24 = puVar24 + 1;
    puVar23 = puVar23 + 5;
  } while (bVar1);
LAB_00b12770:
  uVar15 = *(undefined4 *)(iVar11 + 0x60);
  iVar22 = 0;
  uVar14 = _opd_FUN_00242bf0(uVar15,*(undefined4 *)(puVar5 + 0x16));
  _opd_FUN_002495a8(uVar15,0x7e0ee,uVar14);
  uVar15 = *(undefined4 *)(iVar11 + 0x60);
  uVar14 = _opd_FUN_00242bf0(uVar15,*(undefined4 *)((int)puVar5 + 0xb4));
  _opd_FUN_002495a8(uVar15,0x7e0ee,uVar14);
  uVar15 = *(undefined4 *)(iVar11 + 0x60);
  uVar14 = _opd_FUN_00242bf0(uVar15,*(undefined4 *)(puVar5 + 0x17));
  _opd_FUN_002495a8(uVar15,0x7e0ee,uVar14);
  uVar15 = *(undefined4 *)(iVar11 + 0x60);
  uVar14 = _opd_FUN_00242bf0(uVar15,*(undefined4 *)((int)puVar5 + 0xbc));
  _opd_FUN_002495a8(uVar15,0x7e0ee,uVar14);
  uVar15 = *(undefined4 *)(iVar11 + 0x60);
  uVar14 = _opd_FUN_00242bf0(uVar15,*(undefined4 *)(puVar5 + 0x18));
  _opd_FUN_002495a8(uVar15,0x7e0ee,uVar14);
  uVar15 = *(undefined4 *)(iVar11 + 0x60);
  uVar14 = _opd_FUN_00242bf0(uVar15,*(undefined4 *)((int)puVar5 + 0xc4));
  _opd_FUN_002495a8(uVar15,0x7e0ee,uVar14);
  uVar15 = *(undefined4 *)(iVar11 + 0x60);
  uVar14 = _opd_FUN_00242bf0(uVar15,*(undefined4 *)(puVar5 + 0x19));
  _opd_FUN_002495a8(uVar15,0x7e0ee,uVar14);
  puVar28 = (undefined4 *)(iVar11 + 0x6b0);
  do {
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar13,*puVar28);
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar16,*puVar28);
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar36,*puVar28);
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar37,*puVar28);
    bVar1 = iVar22 != 7;
    iVar22 = iVar22 + 1;
    puVar28 = puVar28 + 5;
  } while (bVar1);
  iVar22 = 0;
  puVar28 = (undefined4 *)(iVar11 + 0x774);
  do {
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar13,*puVar28);
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar16,*puVar28);
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar36,*puVar28);
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar37,*puVar28);
    bVar1 = iVar22 != 0x17;
    iVar22 = iVar22 + 1;
    puVar28 = puVar28 + 5;
  } while (bVar1);
  uVar15 = *(undefined4 *)(iVar11 + 0x60);
  uVar14 = _opd_FUN_00242bf0(uVar15,0x9245d);
  _opd_FUN_002495a8(uVar15,0x516054,uVar14);
  uVar15 = *(undefined4 *)(iVar11 + 0x60);
  uVar14 = _opd_FUN_00242bf0(uVar15,0x9245d);
  _opd_FUN_002495a8(uVar15,0xf7a368,uVar14);
  _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),0x7e0ee,*(undefined4 *)(iVar11 + 0xf70));
  _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),0x7e0ee,*(undefined4 *)(iVar11 + 0xf84));
  _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar13,*(undefined4 *)(iVar11 + 0xf00));
  _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar16,*(undefined4 *)(iVar11 + 0xf00));
  _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar36,*(undefined4 *)(iVar11 + 0xf00));
  _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar13,*(undefined4 *)(iVar11 + 0xf1c));
  _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar16,*(undefined4 *)(iVar11 + 0xf1c));
  _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar36,*(undefined4 *)(iVar11 + 0xf1c));
  _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar37,*(undefined4 *)(iVar11 + 0xf1c));
  _opd_FUN_00248d90(*(undefined4 *)(iVar11 + 0x60));
  if (*(int *)(iVar11 + 0x6c) != 0) {
    iVar22 = 0;
    *(undefined4 *)(puVar5 + 0x1a) = 0x516054;
    *(undefined4 *)((int)puVar5 + 0xd4) = 0x516055;
    *(undefined4 *)(puVar5 + 0x1b) = 0xf7a368;
    *(undefined4 *)((int)puVar5 + 0xdc) = 0xdac8eb;
    puVar28 = (undefined4 *)(iVar11 + 0x958);
    do {
      _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x6c),*(undefined4 *)(puVar5 + 0x1a),*puVar28);
      _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x6c),*(undefined4 *)((int)puVar5 + 0xd4),*puVar28)
      ;
      _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x6c),*(undefined4 *)(puVar5 + 0x1b),*puVar28);
      _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x6c),*(undefined4 *)((int)puVar5 + 0xdc),*puVar28)
      ;
      bVar1 = iVar22 != 0x17;
      iVar22 = iVar22 + 1;
      puVar28 = puVar28 + 5;
    } while (bVar1);
  }
  iVar22 = 0;
  uVar15 = _opd_FUN_00242bf0(*(undefined4 *)(iVar11 + 0x60),0x696217);
  _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar13,uVar15);
  _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar16,uVar15);
  _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar36,uVar15);
  _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar37,uVar15);
  uVar15 = *(undefined4 *)(iVar12 + 0x3c0);
  uVar14 = *(undefined4 *)(iVar12 + 0x3b4);
  uVar38 = *(undefined4 *)(iVar12 + 0x3b8);
  uVar39 = *(undefined4 *)(iVar12 + 0x3bc);
  *(undefined4 *)(puVar5 + 0x1a) = *(undefined4 *)(iVar12 + 0x3b0);
  *(undefined4 *)((int)puVar5 + 0xd4) = uVar14;
  *(undefined4 *)(puVar5 + 0x1b) = uVar38;
  *(undefined4 *)((int)puVar5 + 0xdc) = uVar39;
  *(undefined4 *)(puVar5 + 0x1c) = uVar15;
  uVar15 = _opd_FUN_00242bf0(*(undefined4 *)(iVar11 + 0x60),&DAT_008b0495);
  _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar13,uVar15);
  _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar16,uVar15);
  _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar36,uVar15);
  _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar37,uVar15);
  do {
    uVar15 = _opd_FUN_00242bf0(*(undefined4 *)(iVar11 + 0x60),
                               *(undefined4 *)((int)local_230 + iVar22));
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar13,uVar15);
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar16,uVar15);
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar36,uVar15);
    _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar37,uVar15);
    bVar1 = iVar22 != 0x10;
    iVar22 = iVar22 + 4;
  } while (bVar1);
  uVar15 = _opd_FUN_00242bf0(*(undefined4 *)(iVar11 + 0x60),&DAT_000b942e);
  _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar13,uVar15);
  _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar16,uVar15);
  _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar36,uVar15);
  _opd_FUN_002495a8(*(undefined4 *)(iVar11 + 0x60),uVar37,uVar15);
  uVar13 = *(undefined4 *)(iVar11 + 0x60);
  uVar16 = _opd_FUN_00242bf0(uVar13,&DAT_009c443f);
  _opd_FUN_002495a8(uVar13,0x516054,uVar16);
  uVar13 = *(undefined4 *)(iVar11 + 0x60);
  uVar16 = _opd_FUN_00242bf0(uVar13,&DAT_009c443f);
  _opd_FUN_002495a8(uVar13,0x516055,uVar16);
  uVar13 = *(undefined4 *)(iVar11 + 0x60);
  uVar16 = _opd_FUN_00242bf0(uVar13,&DAT_009a335f);
  _opd_FUN_002495a8(uVar13,0x516054,uVar16);
  uVar13 = *(undefined4 *)(iVar11 + 0x60);
  uVar16 = _opd_FUN_00242bf0(uVar13,&DAT_009a335f);
  _opd_FUN_002495a8(uVar13,0x516055,uVar16);
  uVar13 = *(undefined4 *)(iVar11 + 0x60);
  uVar16 = _opd_FUN_00242bf0(uVar13,0x5850f4);
  _opd_FUN_002495a8(uVar13,0x516054,uVar16);
  uVar13 = *(undefined4 *)(iVar11 + 0x60);
  uVar16 = _opd_FUN_00242bf0(uVar13,0x5850f4);
  _opd_FUN_002495a8(uVar13,0x516055,uVar16);
  uVar13 = *(undefined4 *)(iVar11 + 0x60);
  uVar16 = _opd_FUN_00242bf0(uVar13,0x564014);
  _opd_FUN_002495a8(uVar13,0x516054,uVar16);
  uVar13 = *(undefined4 *)(iVar11 + 0x60);
  uVar16 = _opd_FUN_00242bf0(uVar13,0x564014);
  _opd_FUN_002495a8(uVar13,0x516055,uVar16);
  uVar29 = *(undefined8 *)(iVar11 + 0x20);
  uVar10 = _opd_FUN_00748720();
  if (uVar10 == 0) goto LAB_00b12158;
  lVar21 = (uVar10 & 0xfffffff) << 4;
  lVar25 = 0;
  if (*(ulonglong *)((int)lVar21 + 0x20) == uVar10) {
    lVar25 = lVar21;
  }
  _opd_FUN_000beea8(lVar25,uVar29);
  *(undefined4 *)(iVar11 + 4000) = *(undefined4 *)(puVar6 + -0x7de0);
  uVar13 = _opd_FUN_00036350();
  puVar17 = (undefined *)_opd_FUN_000cdc90(uVar13);
  if (puVar17 != &UNK_00f8c747) {
    uVar13 = _opd_FUN_00036350();
    puVar17 = (undefined *)_opd_FUN_000cdc90(uVar13);
    if (puVar17 == &UNK_00f8c6c7) goto LAB_00b13088;
    uVar13 = _opd_FUN_00036350();
    puVar17 = (undefined *)_opd_FUN_000cdc90(uVar13);
    if (puVar17 != &UNK_00f8ca67) {
      uVar13 = _opd_FUN_00036350();
      puVar17 = (undefined *)_opd_FUN_000cdc90(uVar13);
      if (puVar17 != &UNK_00f8c787) {
        uVar13 = _opd_FUN_00036350();
        puVar17 = (undefined *)_opd_FUN_000cdc90(uVar13);
        if (puVar17 != &UNK_00f8c727) {
          uVar13 = _opd_FUN_00036350();
          _opd_FUN_000cdc90(uVar13);
          goto LAB_00b13088;
        }
      }
    }
  }
  *(undefined4 *)(iVar11 + 4000) = *(undefined4 *)(puVar6 + -0x7b80);
LAB_00b13088:
  _opd_FUN_00b10d28(iVar11);
  _opd_FUN_00b30830(*(undefined4 *)(iVar11 + 0x60),0x57d67b);
  iVar12 = _opd_FUN_00b188b0();
  if (iVar12 != 0) {
    iVar12 = _opd_FUN_00242bf0(*(undefined4 *)(iVar11 + 0x60),0x57d67b);
    uVar13 = *(undefined4 *)(iVar11 + 0x60);
    uVar16 = _opd_FUN_00242bf0(uVar13,0x784aa2);
    uVar13 = _opd_FUN_00255678(uVar13,uVar16);
    _opd_FUN_00255960(iVar12,uVar13);
    iVar22 = *(ushort *)(iVar12 + 0x1a) - 1;
    if (-1 < iVar22) {
      uVar3 = *(uint *)(puVar6 + -0x7b6c);
      puVar5[0x3b] = uVar8 - 0x180 & 0xffffffff;
      puVar5[0x3c] = uVar8 - 400 & 0xffffffff;
      puVar5[0x3a] = uVar8 - 0x170 & 0xffffffff;
      puVar5[0x3d] = (ulonglong)*(uint *)(puVar6 + -0x7b78);
      puVar5[0x39] = uVar8 - 0x160 & 0xffffffff;
      iVar26 = iVar12 + 0x1c;
      uVar10 = (ulonglong)*(uint *)(puVar6 + -0x7b74);
      puVar5[0x3f] = (ulonglong)*(uint *)(puVar6 + -0x7b70);
      puVar5[0x3e] = uVar10;
      do {
        puVar28 = (undefined4 *)((uint)uVar10 & 0xfffffff0);
        uVar15 = *puVar28;
        uVar14 = puVar28[1];
        uVar38 = puVar28[2];
        uVar39 = puVar28[3];
        iVar27 = *(ushort *)(iVar26 + 4) - 1;
        *(undefined4 *)((int)puVar5 + 0xd4) = 0xbf800000;
        *(undefined4 *)(puVar5 + 0x1a) = 0;
        *(undefined4 *)(puVar5 + 0x1b) = 0;
        puVar28 = (undefined4 *)((uint)puVar5[0x3d] & 0xfffffff0);
        uVar13 = *puVar28;
        uVar16 = puVar28[1];
        uVar36 = puVar28[2];
        uVar37 = puVar28[3];
        puVar28 = (undefined4 *)(uVar3 & 0xfffffff0);
        uVar45 = *puVar28;
        uVar46 = puVar28[1];
        uVar47 = puVar28[2];
        uVar48 = puVar28[3];
        puVar28 = (undefined4 *)((uint)puVar5[0x3f] & 0xfffffff0);
        uVar49 = *puVar28;
        uVar54 = puVar28[1];
        uVar55 = puVar28[2];
        uVar56 = puVar28[3];
        *(undefined4 *)((int)puVar5 + 0xdc) = 0x3f800000;
        puVar28 = (undefined4 *)((uint)puVar5[0x3c] & 0xfffffff0);
        *puVar28 = uVar13;
        puVar28[1] = uVar16;
        puVar28[2] = uVar36;
        puVar28[3] = uVar37;
        puVar28 = (undefined4 *)((uint)puVar5[0x3b] & 0xfffffff0);
        *puVar28 = uVar15;
        puVar28[1] = uVar14;
        puVar28[2] = uVar38;
        puVar28[3] = uVar39;
        puVar28 = (undefined4 *)((uint)puVar5[0x3a] & 0xfffffff0);
        *puVar28 = uVar49;
        puVar28[1] = uVar54;
        puVar28[2] = uVar55;
        puVar28[3] = uVar56;
        puVar28 = (undefined4 *)((uint)puVar5[0x39] & 0xfffffff0);
        *puVar28 = uVar45;
        puVar28[1] = uVar46;
        puVar28[2] = uVar47;
        puVar28[3] = uVar48;
        if (-1 < iVar27) {
          *(undefined4 *)(puVar5 + 0x40) = 0;
          dVar32 = (double)*(float *)(puVar5 + 0x40);
          lVar25 = uVar8 - 0x290;
          auVar62 = *(undefined1 (*) [16])(puVar5 + 0x1a);
          if (iVar27 == 0) goto LAB_00b13758;
          do {
            dVar33 = (double)*(float *)(puVar6 + -0x7f98);
            fVar2 = *(float *)(puVar6 + -0x7f90);
            dVar34 = dVar33;
            if (iVar27 != 1) {
              *(float *)(puVar5 + 0x38) = (float)dVar32;
              auVar35 = ZEXT416(0xffffffff);
              auVar43 = auVar35 | auVar35 << 0x20 | auVar35 << 0x40 | auVar35 << 0x60;
              auVar35 = ZEXT416(1);
              auVar42 = auVar35 | auVar35 << 0x20 | auVar35 << 0x40 | auVar35 << 0x60;
              auVar44 = vectorShiftLeftIntegerWord(auVar43,auVar43);
              auVar43 = loadVectorElementWordIndexed(uVar8 - 0x300,0x1c0);
              auVar35 = ZEXT416(2);
              auVar40 = auVar35 | auVar35 << 0x20 | auVar35 << 0x40 | auVar35 << 0x60;
              auVar43 = auVar43 >> 0x60;
              auVar41 = auVar43 & (undefined1  [16])0xffffffff | auVar43 << 0x20 | auVar43 << 0x40 |
                        auVar43 << 0x60;
              auVar43 = vectorMultiplyAddFloatingPoint
                                  (auVar41,*(undefined1 (*) [16])
                                            (*(uint *)(puVar6 + -0x7b68) & 0xfffffff0),
                                   (undefined1  [16])0x0);
              auVar35 = vectorConditionalSelect
                                  (*(undefined1 (*) [16])(*(uint *)(puVar6 + -0x7b64) & 0xfffffff0),
                                   auVar43,auVar44);
              auVar43 = vectorAddFloatingPoint(auVar43,auVar35);
              auVar35 = ZEXT416(3);
              auVar50 = vectorConvertToSignedFixedPointWordSaturate(auVar43,0);
              auVar43 = vectorConvertFromSignedFixedPointWord(auVar50,0);
              auVar51 = vectorLogicalAnd(auVar50,auVar35 | auVar35 << 0x20 | auVar35 << 0x40 |
                                                 auVar35 << 0x60);
              auVar35._0_4_ = auVar51._0_4_ + auVar42._0_4_;
              auVar35._8_8_ = in_vs37._8_8_;
              auVar35._4_4_ = auVar51._4_4_ + auVar42._4_4_;
              auVar50._0_8_ = auVar35._0_8_;
              auVar50._8_4_ = auVar51._8_4_ + auVar42._8_4_;
              auVar50._12_4_ = auVar51._12_4_ + auVar42._12_4_;
              auVar35 = vectorLogicalAnd(auVar51,auVar42);
              auVar52 = vectorLogicalAnd(auVar51,auVar40);
              auVar51 = vectorLogicalAnd(auVar50,auVar42);
              auVar42 = vectorCompareEqualToUnsignedWord(auVar35,(undefined1  [16])0x0);
              auVar35 = vectorNegativeMultiplySubtractFloatingPoint
                                  (auVar43,*(undefined1 (*) [16])
                                            (*(uint *)(puVar6 + -0x7b60) & 0xfffffff0),auVar41);
              auVar40 = vectorLogicalAnd(auVar50,auVar40);
              auVar41 = vectorCompareEqualToUnsignedWord(auVar51,(undefined1  [16])0x0);
              auVar53 = vectorCompareEqualToUnsignedWord(auVar52,(undefined1  [16])0x0);
              auVar40 = vectorCompareEqualToUnsignedWord(auVar40,(undefined1  [16])0x0);
              auVar43 = vectorNegativeMultiplySubtractFloatingPoint
                                  (auVar43,*(undefined1 (*) [16])
                                            (*(uint *)(puVar6 + -0x7b5c) & 0xfffffff0),auVar35);
              auVar35 = vectorMultiplyAddFloatingPoint(auVar43,auVar43,(undefined1  [16])0x0);
              auVar52 = vectorMultiplyAddFloatingPoint
                                  (auVar35,*(undefined1 (*) [16])
                                            (*(uint *)(puVar6 + -0x7b58) & 0xfffffff0),
                                   *(undefined1 (*) [16])(*(uint *)(puVar6 + -0x7b54) & 0xfffffff0))
              ;
              auVar51 = vectorMultiplyAddFloatingPoint
                                  (auVar35,*(undefined1 (*) [16])
                                            (*(uint *)(puVar6 + -0x7b50) & 0xfffffff0),
                                   *(undefined1 (*) [16])(*(uint *)(puVar6 + -0x7b4c) & 0xfffffff0))
              ;
              auVar50 = vectorMultiplyAddFloatingPoint(auVar35,auVar43,(undefined1  [16])0x0);
              auVar52 = vectorMultiplyAddFloatingPoint
                                  (auVar52,auVar35,
                                   *(undefined1 (*) [16])(*(uint *)(puVar6 + -0x7b48) & 0xfffffff0))
              ;
              auVar51 = vectorMultiplyAddFloatingPoint
                                  (auVar51,auVar35,
                                   *(undefined1 (*) [16])(*(uint *)(puVar6 + -0x7b44) & 0xfffffff0))
              ;
              auVar52 = vectorMultiplyAddFloatingPoint
                                  (auVar52,auVar35,
                                   *(undefined1 (*) [16])(*(uint *)(puVar6 + -0x7b40) & 0xfffffff0))
              ;
              auVar43 = vectorMultiplyAddFloatingPoint(auVar51,auVar50,auVar43);
              auVar35 = vectorConditionalSelect(auVar52,auVar43,auVar42);
              auVar50 = vectorConditionalSelect(auVar52,auVar43,auVar41);
              auVar43 = vectorConditionalSelect(auVar44 ^ auVar35,auVar35,auVar53);
              auVar50 = vectorConditionalSelect(auVar44 ^ auVar50,auVar50,auVar40);
              auVar35 = vectorConditionalSelect
                                  ((undefined1  [16])0x0,auVar50,
                                   *(undefined1 (*) [16])(*(uint *)(puVar6 + -0x7b38) & 0xfffffff0))
              ;
              auVar40 = vectorConditionalSelect
                                  ((undefined1  [16])0x0,
                                   auVar43 ^ *(undefined1 (*) [16])
                                              (*(uint *)(puVar6 + -0x7b3c) & 0xfffffff0),
                                   *(undefined1 (*) [16])(*(uint *)(puVar6 + -0x7b38) & 0xfffffff0))
              ;
              vectorConditionalSelect
                        (auVar35,auVar43,
                         *(undefined1 (*) [16])(*(uint *)(puVar6 + -0x7b34) & 0xfffffff0));
              vectorConditionalSelect
                        (auVar40,auVar50,
                         *(undefined1 (*) [16])(*(uint *)(puVar6 + -0x7b34) & 0xfffffff0));
              auVar35 = *(undefined1 (*) [16])(puVar5 + 0x2e);
              auVar51 = *(undefined1 (*) [16])(*(uint *)(puVar6 + -0x7b30) & 0xfffffff0);
              auVar42 = *(undefined1 (*) [16])(*(uint *)(puVar6 + -0x7b2c) & 0xfffffff0);
              auVar43 = vectorPermute(auVar35,auVar35,auVar51);
              auVar50 = vectorPermute(auVar35,auVar35,auVar42);
              auVar44 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar5 + 0x1e),auVar43,
                                   (undefined1  [16])0x0);
              auVar40 = *(undefined1 (*) [16])(*(uint *)(puVar6 + -0x7b28) & 0xfffffff0);
              auVar41 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar5 + 0x20),auVar50,
                                   (undefined1  [16])0x0);
              auVar50 = *(undefined1 (*) [16])(*(uint *)(puVar6 + -0x7b24) & 0xfffffff0);
              auVar43 = vectorPermute(auVar35,auVar35,auVar40);
              auVar35 = vectorPermute(auVar35,auVar35,auVar50);
              auVar59 = vectorPermute(auVar62,auVar62,auVar51);
              auVar60 = vectorPermute(auVar62,auVar62,auVar42);
              auVar58 = vectorPermute(auVar62,auVar62,auVar50);
              auVar57 = vectorPermute(auVar62,auVar62,auVar40);
              dVar32 = (double)(float)(dVar32 + (double)*(float *)(puVar6 + -0x7fa0));
              auVar43 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar5 + 0x22),auVar43,auVar44);
              auVar35 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar5 + 0x24),auVar35,auVar41);
              vectorAddFloatingPoint(auVar43,auVar35);
              auVar35 = *(undefined1 (*) [16])(puVar5 + 0x30);
              auVar43 = vectorPermute(auVar35,auVar35,auVar51);
              auVar44 = vectorPermute(auVar35,auVar35,auVar42);
              auVar41 = vectorPermute(auVar35,auVar35,auVar50);
              auVar43 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar5 + 0x1e),auVar43,
                                   (undefined1  [16])0x0);
              auVar35 = vectorPermute(auVar35,auVar35,auVar40);
              auVar44 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar5 + 0x20),auVar44,
                                   (undefined1  [16])0x0);
              auVar43 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar5 + 0x22),auVar35,auVar43);
              auVar35 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar5 + 0x24),auVar41,auVar44);
              vectorAddFloatingPoint(auVar43,auVar35);
              auVar53 = *(undefined1 (*) [16])(puVar5 + 0x32);
              auVar52 = *(undefined1 (*) [16])(puVar5 + 0x34);
              auVar35 = vectorPermute(auVar53,auVar53,auVar51);
              auVar43 = vectorPermute(auVar53,auVar53,auVar42);
              auVar44 = vectorPermute(auVar52,auVar52,auVar51);
              auVar41 = vectorPermute(auVar52,auVar52,auVar42);
              auVar51 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar5 + 0x1e),auVar35,
                                   (undefined1  [16])0x0);
              auVar61 = vectorPermute(auVar53,auVar53,auVar50);
              auVar35 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar5 + 0x20),auVar43,
                                   (undefined1  [16])0x0);
              auVar42 = vectorPermute(auVar52,auVar52,auVar50);
              in_vs37 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar5 + 0x1e),auVar44,
                                   (undefined1  [16])0x0);
              auVar43 = vectorPermute(auVar53,auVar53,auVar40);
              auVar50 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar5 + 0x20),auVar41,
                                   (undefined1  [16])0x0);
              auVar40 = vectorPermute(auVar52,auVar52,auVar40);
              auVar43 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar5 + 0x22),auVar43,auVar51);
              auVar35 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar5 + 0x24),auVar61,auVar35);
              auVar41 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar5 + 0x22),auVar40,in_vs37);
              auVar40 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar5 + 0x24),auVar42,auVar50);
              auVar35 = vectorAddFloatingPoint(auVar43,auVar35);
              vectorAddFloatingPoint(auVar41,auVar40);
              *(undefined1 (*) [16])lVar25 = auVar35;
              auVar35 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar5 + 0x28),auVar60,
                                   (undefined1  [16])0x0);
              auVar43 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar5 + 0x26),auVar59,
                                   (undefined1  [16])0x0);
              auVar35 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar5 + 0x2c),auVar58,auVar35);
              auVar43 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar5 + 0x2a),auVar57,auVar43);
              auVar35 = vectorAddFloatingPoint(auVar43,auVar35);
              *(undefined1 (*) [16])(puVar5 + 0x16) = auVar35;
              dVar34 = -(double)(float)((double)*(float *)(puVar5 + 0x16) * dVar33 - dVar33);
              fVar2 = (float)((double)*(float *)((int)puVar5 + 0xb4) * dVar33 + dVar33);
            }
            uVar13 = *(undefined4 *)(iVar11 + 0x60);
            uVar7 = 0x40;
            dVar33 = (double)fVar2;
            while( true ) {
              iVar18 = _opd_FUN_00255710(uVar13,iVar12);
              if (iVar18 != 0) {
                fVar2 = *(float *)(puVar6 + -0x7f88);
                uVar4 = *(uint *)(iVar18 + 0x18);
                puVar5[0x41] = (ulonglong)*(uint *)(iVar18 + 0x10);
                uVar10 = puVar5[0x41];
                puVar5[0x41] = (ulonglong)uVar4;
                uVar31 = puVar5[0x41];
                uVar4 = *(uint *)(iVar18 + 0x14);
                puVar5[0x41] = (ulonglong)*(uint *)(iVar18 + 0x1c);
                uVar30 = puVar5[0x41];
                puVar5[0x41] = (ulonglong)uVar4;
                if ((float)(dVar34 * (double)(longlong)uVar10 + (double)(longlong)uVar31) * fVar2 <
                    *(float *)(puVar6 + -0x7f90)) {
                  uVar19 = *(undefined2 *)((int)puVar5 + 0x1b2);
                }
                else {
                  uVar19 = *(undefined2 *)((int)puVar5 + 0x1b2);
                }
                if ((float)(dVar33 * (double)(longlong)puVar5[0x41] + (double)(longlong)uVar30) *
                    fVar2 < *(float *)(puVar6 + -0x7f90)) {
                  uVar20 = *(undefined2 *)((int)puVar5 + 0x1b2);
                }
                else {
                  uVar20 = *(undefined2 *)((int)puVar5 + 0x1b2);
                }
                _opd_FUN_00253ae8(iVar12,uVar19,uVar20,0,iVar27);
              }
              _opd_FUN_00253e60(iVar12,lVar25,0,0);
              uVar7 = *(uint *)(puVar5 + 0xe) & 0xffffff00 | uVar7;
              *(uint *)(puVar5 + 0xe) = uVar7;
              _opd_FUN_00253c60(iVar12,uVar7,0xf,0,iVar27);
              bVar1 = iVar27 == 0;
              iVar27 = iVar27 + -1;
              if (bVar1) goto LAB_00b137f4;
              if (iVar27 != 0) break;
LAB_00b13758:
              uVar13 = *(undefined4 *)(iVar11 + 0x60);
              if (iVar12 == 0) {
                _opd_FUN_00253e60(0,lVar25,0,0);
                uVar7 = *(uint *)(puVar5 + 0xe) & 0xffffff00 | 0x80;
                *(uint *)(puVar5 + 0xe) = uVar7;
                _opd_FUN_00253c60(0,uVar7,0xf,0,0);
                goto LAB_00b137f4;
              }
              dVar34 = (double)*(float *)(puVar6 + -0x7f98);
              uVar7 = 0x80;
              dVar33 = dVar34;
            }
          } while( true );
        }
LAB_00b137f4:
        bVar1 = iVar22 == 0;
        iVar22 = iVar22 + -1;
        if (bVar1) break;
        iVar26 = iVar26 + 0xc;
        uVar10 = puVar5[0x3e];
      } while( true );
    }
  }
  _opd_FUN_00253c60(*(undefined4 *)(iVar11 + 0x70),0x60,1,0,0);
  uVar13 = _opd_FUN_00242bf0(*(undefined4 *)(iVar11 + 0x60),0xd946c2);
  _opd_FUN_00253de0(uVar13,iVar11 + 0x1120,iVar11 + 0x1122,0,0);
  fVar2 = *(float *)(puVar6 + -0x7f90);
  *(undefined4 *)(iVar11 + 0x1124) = 0;
  _opd_FUN_0024d078((double)fVar2,*(undefined4 *)(iVar11 + 0x60));
  if (*(int *)(iVar11 + 0x68) != 0) {
    _opd_FUN_0024d078((double)*(float *)(puVar6 + -0x7f90),*(int *)(iVar11 + 0x68));
  }
  if (*(int *)(iVar11 + 0x6c) != 0) {
    _opd_FUN_0024d078((double)*(float *)(puVar6 + -0x7f90),*(int *)(iVar11 + 0x6c));
  }
  **(int **)(puVar6 + -0x8000) = iVar11;
  return lVar9;
}

