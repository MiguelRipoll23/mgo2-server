// hook SITE 0x00bd99e8

longlong _opd_FUN_00bd8148(void)

{
  bool bVar1;
  float fVar2;
  float fVar3;
  float fVar4;
  uint uVar5;
  uint uVar6;
  ulonglong *puVar7;
  float fVar8;
  float fVar9;
  undefined *puVar10;
  ushort uVar12;
  uint uVar11;
  ulonglong uVar13;
  int iVar15;
  longlong lVar14;
  int iVar16;
  undefined4 uVar17;
  int iVar18;
  uint *puVar19;
  int iVar20;
  undefined4 uVar21;
  undefined *puVar22;
  uint *puVar23;
  uint *puVar24;
  uint *puVar25;
  undefined4 uVar26;
  int iVar27;
  undefined4 uVar28;
  ulonglong uVar29;
  ulonglong unaff_r14;
  ulonglong unaff_r15;
  longlong lVar30;
  ulonglong unaff_r16;
  ulonglong unaff_r17;
  ulonglong unaff_r18;
  ulonglong unaff_r19;
  ulonglong unaff_r20;
  ulonglong uVar31;
  int iVar32;
  ulonglong unaff_r21;
  ulonglong unaff_r22;
  ulonglong unaff_r23;
  ulonglong unaff_r24;
  ulonglong unaff_r25;
  ulonglong unaff_r26;
  ulonglong unaff_r27;
  ulonglong unaff_r28;
  undefined2 uVar33;
  ulonglong unaff_r29;
  undefined4 *puVar34;
  undefined2 uVar35;
  ulonglong unaff_r30;
  ulonglong unaff_r31;
  undefined8 uVar36;
  int *piVar37;
  ulonglong in_LR;
  ulonglong uVar38;
  ulonglong in_f29;
  double dVar39;
  ulonglong in_f30;
  double dVar40;
  double dVar41;
  ulonglong in_f31;
  undefined4 uVar43;
  undefined1 auVar42 [16];
  undefined4 uVar44;
  undefined4 uVar45;
  undefined4 uVar46;
  undefined1 auVar47 [16];
  undefined1 in_vs37 [16];
  undefined1 auVar48 [16];
  undefined1 auVar49 [16];
  undefined1 auVar50 [16];
  undefined1 auVar51 [16];
  undefined4 uVar52;
  undefined4 uVar53;
  undefined4 uVar54;
  undefined4 uVar55;
  undefined4 uVar56;
  undefined4 uVar61;
  undefined4 uVar62;
  undefined4 uVar63;
  undefined1 auVar57 [16];
  undefined1 auVar58 [16];
  undefined1 auVar59 [16];
  undefined1 auVar60 [16];
  undefined1 auVar64 [16];
  undefined1 auVar65 [16];
  undefined4 in_vr28_32_0;
  undefined4 in_vr28_32_1;
  undefined4 in_vr28_32_2;
  undefined4 in_vr28_32_3;
  undefined1 auVar66 [16];
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
  undefined4 local_360 [8];
  undefined4 local_340 [24];
  undefined1 local_2e0 [16];
  undefined1 local_2d0 [16];
  undefined4 uStack_2c0;
  undefined4 uStack_2bc;
  undefined4 uStack_2b8;
  undefined4 uStack_2b4;
  undefined4 uStack_2b0;
  undefined4 uStack_2ac;
  undefined4 uStack_2a8;
  undefined4 uStack_2a4;
  int local_160;
  
  uVar13 = ZEXT48(&stack0x00000000);
  puVar7 = (ulonglong *)(uVar13 - 0x710);
  *puVar7 = uVar13;
  *(undefined4 *)(puVar7 + 0xc4) = in_vr28_32_0;
  *(undefined4 *)((int)puVar7 + 0x624) = in_vr28_32_1;
  *(undefined4 *)(puVar7 + 0xc5) = in_vr28_32_2;
  *(undefined4 *)((int)puVar7 + 0x62c) = in_vr28_32_3;
  *(undefined4 *)(puVar7 + 0xc6) = in_vr29_32_0;
  *(undefined4 *)((int)puVar7 + 0x634) = in_vr29_32_1;
  *(undefined4 *)(puVar7 + 199) = in_vr29_32_2;
  *(undefined4 *)((int)puVar7 + 0x63c) = in_vr29_32_3;
  *(undefined4 *)(puVar7 + 200) = in_vr30_32_0;
  *(undefined4 *)((int)puVar7 + 0x644) = in_vr30_32_1;
  *(undefined4 *)(puVar7 + 0xc9) = in_vr30_32_2;
  *(undefined4 *)((int)puVar7 + 0x64c) = in_vr30_32_3;
  *(undefined4 *)(puVar7 + 0xca) = in_vr31_32_0;
  *(undefined4 *)((int)puVar7 + 0x654) = in_vr31_32_1;
  *(undefined4 *)(puVar7 + 0xcb) = in_vr31_32_2;
  *(undefined4 *)((int)puVar7 + 0x65c) = in_vr31_32_3;
  puVar7[0xdd] = unaff_r30;
  puVar10 = PTR_DAT_0121bdc4;
  puVar7[0xde] = unaff_r31;
  puVar7[0xcd] = unaff_r14;
  puVar7[0xdb] = unaff_r28;
  puVar7[0xdf] = in_f29;
  uVar36 = *(undefined8 *)(puVar10 + -0x7fb8);
  puVar7[0xe0] = in_f30;
  puVar7[0xe1] = in_f31;
  puVar7[0xce] = unaff_r15;
  puVar7[0xcf] = unaff_r16;
  puVar7[0xd0] = unaff_r17;
  puVar7[0xd1] = unaff_r18;
  puVar7[0xd2] = unaff_r19;
  puVar7[0xd3] = unaff_r20;
  puVar7[0xd4] = unaff_r21;
  puVar7[0xd5] = unaff_r22;
  puVar7[0xd6] = unaff_r23;
  puVar7[0xd7] = unaff_r24;
  puVar7[0xd8] = unaff_r25;
  puVar7[0xd9] = unaff_r26;
  puVar7[0xda] = unaff_r27;
  puVar7[0xdc] = unaff_r29;
  puVar7[0xe4] = in_LR;
  iVar15 = _opd_FUN_000be778(0x1160,uVar36);
  if (iVar15 == 0) {
    return 0;
  }
  lVar14 = _opd_FUN_000bead8(iVar15,uVar36,*(undefined4 *)(puVar10 + -0x7fb0),
                             *(undefined4 *)(puVar10 + -0x7fac));
  if (lVar14 == 0) {
LAB_00bd8218:
    _opd_FUN_000c1e00(iVar15);
    return 0;
  }
  iVar16 = _opd_FUN_00242398(&DAT_00397541,2,2,0xc4,1,0,0);
  *(int *)(iVar15 + 0x60) = iVar16;
  if (iVar16 == 0) goto LAB_00bd8218;
  _opd_FUN_0024d078((double)*(float *)(puVar10 + -0x7fdc),iVar16);
  iVar16 = _opd_FUN_00b1e138();
  if (iVar16 != 0) {
    iVar16 = _opd_FUN_00242398(0x2ea916,2,2,0xc4,1,0,0);
    *(int *)(iVar15 + 0x68) = iVar16;
    if (iVar16 == 0) goto LAB_00bd8218;
    uVar17 = _opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),0x361a4e);
    _opd_FUN_00243718(iVar16,uVar17);
    _opd_FUN_0024d078((double)*(float *)(puVar10 + -0x7fdc),*(undefined4 *)(iVar15 + 0x68));
  }
  iVar16 = _opd_FUN_00b1e138();
  if (((iVar16 != 0) || (iVar16 = _opd_FUN_00b18a30(), iVar16 != 0)) ||
     (iVar16 = _opd_FUN_00b18830(), iVar16 != 0)) {
    iVar16 = _opd_FUN_00242398(0x399138,2,2,0xc4,1,0,0);
    *(int *)(iVar15 + 0x6c) = iVar16;
    if (iVar16 == 0) goto LAB_00bd8218;
    uVar17 = _opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),0x361a4e);
    _opd_FUN_00243718(iVar16,uVar17);
    _opd_FUN_0024d078((double)*(float *)(puVar10 + -0x7fdc),*(undefined4 *)(iVar15 + 0x6c));
  }
  _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0x1c090);
  uVar17 = _opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),0x1c090);
  *(undefined4 *)(iVar15 + 0x70) = uVar17;
  uVar17 = _opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),0x361a4e);
  *(undefined4 *)(iVar15 + 0x74) = uVar17;
  uVar17 = _opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),0xc95deb);
  *(undefined4 *)(iVar15 + 0x78) = uVar17;
  uVar17 = _opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),0x85e701);
  *(undefined4 *)(iVar15 + 0x7c) = uVar17;
  uVar17 = _opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),0xbdb6a4);
  *(undefined4 *)(iVar15 + 0x80) = uVar17;
  uVar17 = _opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),&DAT_00b4d7a6);
  *(undefined4 *)(iVar15 + 0x84) = uVar17;
  uVar17 = _opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),0x945027);
  *(undefined4 *)(iVar15 + 0x88) = uVar17;
  uVar17 = _opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),0xab6684);
  *(undefined4 *)(iVar15 + 0x8c) = uVar17;
  _opd_FUN_00253de0(*(undefined4 *)(iVar15 + 0x7c),iVar15 + 0xfa4,iVar15 + 0xfa6,0,0);
  puVar19 = *(uint **)(iVar15 + 0x8c);
  if ((puVar19 != (uint *)0x0) && ((*puVar19 & 0x400000) != 0)) {
    *puVar19 = *puVar19 & 0xffbfffff | 0x40000000;
  }
  iVar16 = *(int *)(puVar10 + -0x7ffc);
  uVar31 = uVar13 - 0x2a0;
  piVar37 = (int *)(iVar15 + 0xb8);
  iVar20 = 0;
  _opd_FUN_00f9ac38(uVar31,iVar16,0x140);
  while( true ) {
    iVar32 = (int)uVar31;
    puVar34 = (undefined4 *)(iVar20 + iVar32);
    iVar18 = _opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),*puVar34);
    *piVar37 = iVar18;
    iVar18 = _opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),puVar34[1]);
    piVar37[1] = iVar18;
    iVar18 = _opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),puVar34[2]);
    piVar37[2] = iVar18;
    iVar18 = _opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),puVar34[3]);
    piVar37[3] = iVar18;
    iVar18 = _opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),puVar34[4]);
    puVar19 = (uint *)*piVar37;
    piVar37[4] = iVar18;
    if ((puVar19 != (uint *)0x0) && ((*puVar19 & 0x400000) != 0)) {
      *puVar19 = *puVar19 & 0xffbfffff | 0x40000000;
    }
    puVar19 = (uint *)piVar37[2];
    if ((puVar19 != (uint *)0x0) && ((*puVar19 & 0x400000) == 0)) {
      *puVar19 = *puVar19 | 0x40400000;
    }
    puVar19 = (uint *)piVar37[4];
    if ((puVar19 != (uint *)0x0) && ((*puVar19 & 0x400000) != 0)) {
      *puVar19 = *puVar19 & 0xffbfffff | 0x40000000;
    }
    bVar1 = iVar20 == 300;
    iVar20 = iVar20 + 0x14;
    if (bVar1) break;
    piVar37 = piVar37 + 0xf;
  }
  *(undefined4 *)(puVar7 + 0x7a) = 0xe247df;
  *(undefined4 *)((int)puVar7 + 0x3d4) = 0xe247e0;
  puVar19 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),0xe247df);
  *(uint **)(iVar15 + 0x47c) = puVar19;
  if ((puVar19 != (uint *)0x0) && ((*puVar19 & 0x400000) != 0)) {
    *puVar19 = *puVar19 & 0xffbfffff | 0x40000000;
  }
  puVar19 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),
                                      *(undefined4 *)((int)puVar7 + 0x3d4));
  *(uint **)(iVar15 + 0x490) = puVar19;
  if ((puVar19 != (uint *)0x0) && ((*puVar19 & 0x400000) != 0)) {
    *puVar19 = *puVar19 & 0xffbfffff | 0x40000000;
  }
  puVar19 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),0x83d782);
  *(uint **)(iVar15 + 0xf68) = puVar19;
  if ((puVar19 != (uint *)0x0) && ((*puVar19 & 0x400000) != 0)) {
    *puVar19 = *puVar19 & 0xffbfffff | 0x40000000;
  }
  iVar20 = _opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),0x73bba1);
  *(int *)(iVar15 + 0xf7c) = iVar20;
  iVar20 = _opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),0x83bba1);
  *(int *)(iVar15 + 0xf90) = iVar20;
  puVar19 = *(uint **)(iVar15 + 0xf7c);
  if ((puVar19 != (uint *)0x0) && ((*puVar19 & 0x400000) != 0)) {
    *puVar19 = *puVar19 & 0xffbfffff | 0x40000000;
  }
  puVar19 = *(uint **)(iVar15 + 0xf90);
  if ((puVar19 != (uint *)0x0) && ((*puVar19 & 0x400000) != 0)) {
    *puVar19 = *puVar19 & 0xffbfffff | 0x40000000;
  }
  *(undefined4 *)(puVar7 + 0x7a) = 0x63d184;
  *(undefined4 *)((int)puVar7 + 0x3d4) = 0xa1b47c;
  puVar19 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),0x63d184);
  *(uint **)(iVar15 + 0x4a8) = puVar19;
  if ((puVar19 != (uint *)0x0) && ((*puVar19 & 0x400000) != 0)) {
    *puVar19 = *puVar19 & 0xffbfffff | 0x40000000;
  }
  puVar19 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),
                                      *(undefined4 *)((int)puVar7 + 0x3d4));
  *(uint **)(iVar15 + 0x4bc) = puVar19;
  if ((puVar19 != (uint *)0x0) && ((*puVar19 & 0x400000) != 0)) {
    *puVar19 = *puVar19 & 0xffbfffff | 0x40000000;
  }
  uVar17 = *(undefined4 *)(iVar16 + 0x144);
  iVar20 = 0;
  uVar21 = *(undefined4 *)(iVar16 + 0x148);
  uVar26 = *(undefined4 *)(iVar16 + 0x14c);
  uVar28 = *(undefined4 *)(iVar16 + 0x150);
  uVar43 = *(undefined4 *)(iVar16 + 0x154);
  uVar44 = *(undefined4 *)(iVar16 + 0x158);
  uVar45 = *(undefined4 *)(iVar16 + 0x15c);
  uVar46 = *(undefined4 *)(iVar16 + 0x160);
  uVar52 = *(undefined4 *)(iVar16 + 0x164);
  uVar53 = *(undefined4 *)(iVar16 + 0x168);
  uVar54 = *(undefined4 *)(iVar16 + 0x16c);
  uVar55 = *(undefined4 *)(iVar16 + 0x170);
  uVar56 = *(undefined4 *)(iVar16 + 0x174);
  uVar61 = *(undefined4 *)(iVar16 + 0x178);
  uVar62 = *(undefined4 *)(iVar16 + 0x17c);
  *(undefined4 *)(puVar7 + 0x8e) = *(undefined4 *)(iVar16 + 0x140);
  *(undefined4 *)((int)puVar7 + 0x474) = uVar17;
  *(undefined4 *)(puVar7 + 0x8f) = uVar21;
  *(undefined4 *)((int)puVar7 + 0x47c) = uVar26;
  *(undefined4 *)(puVar7 + 0x90) = uVar28;
  *(undefined4 *)((int)puVar7 + 0x484) = uVar43;
  *(undefined4 *)(puVar7 + 0x91) = uVar44;
  *(undefined4 *)((int)puVar7 + 0x48c) = uVar45;
  *(undefined4 *)(puVar7 + 0x92) = uVar46;
  *(undefined4 *)((int)puVar7 + 0x494) = uVar52;
  *(undefined4 *)(puVar7 + 0x93) = uVar53;
  *(undefined4 *)((int)puVar7 + 0x49c) = uVar54;
  *(undefined4 *)(puVar7 + 0x94) = uVar55;
  *(undefined4 *)((int)puVar7 + 0x4a4) = uVar56;
  *(undefined4 *)(puVar7 + 0x95) = uVar61;
  *(undefined4 *)((int)puVar7 + 0x4ac) = uVar62;
  puVar34 = (undefined4 *)(iVar15 + 0x4d4);
  do {
    puVar19 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),
                                        *(undefined4 *)(iVar20 + iVar32));
    bVar1 = iVar20 != 0x3c;
    *puVar34 = puVar19;
    if ((puVar19 != (uint *)0x0) && ((*puVar19 & 0x400000) != 0)) {
      *puVar19 = *puVar19 & 0xffbfffff | 0x40000000;
    }
    iVar20 = iVar20 + 4;
    puVar34 = puVar34 + 5;
  } while (bVar1);
  iVar20 = 0;
  _opd_FUN_00f9ac38(uVar31,iVar16 + 0x180,0x60);
  puVar34 = (undefined4 *)(iVar15 + 0x76c);
  do {
    puVar19 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),
                                        *(undefined4 *)(iVar20 + iVar32));
    bVar1 = iVar20 != 0x5c;
    *puVar34 = puVar19;
    if ((puVar19 != (uint *)0x0) && ((*puVar19 & 0x400000) != 0)) {
      *puVar19 = *puVar19 & 0xffbfffff | 0x40000000;
    }
    iVar20 = iVar20 + 4;
    puVar34 = puVar34 + 5;
  } while (bVar1);
  *(undefined4 *)(puVar7 + 0x7a) = 0x8fb5e3;
  *(undefined4 *)((int)puVar7 + 0x3d4) = 0x9fb5e3;
  *(undefined4 *)(puVar7 + 0x7b) = 0xafb5e3;
  puVar19 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),0x8fb5e3);
  if ((puVar19 != (uint *)0x0) && ((*puVar19 & 0x400000) != 0)) {
    *puVar19 = *puVar19 & 0xffbfffff | 0x40000000;
  }
  puVar19 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),
                                      *(undefined4 *)((int)puVar7 + 0x3d4));
  if ((puVar19 != (uint *)0x0) && ((*puVar19 & 0x400000) != 0)) {
    *puVar19 = *puVar19 & 0xffbfffff | 0x40000000;
  }
  puVar19 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),*(undefined4 *)(puVar7 + 0x7b))
  ;
  if ((puVar19 != (uint *)0x0) && ((*puVar19 & 0x400000) != 0)) {
    *puVar19 = *puVar19 & 0xffbfffff | 0x40000000;
  }
  iVar18 = 0;
  uVar17 = *(undefined4 *)(iVar16 + 0x1e4);
  uVar21 = *(undefined4 *)(iVar16 + 0x1e8);
  uVar26 = *(undefined4 *)(iVar16 + 0x1ec);
  uVar28 = *(undefined4 *)(iVar16 + 0x1f0);
  uVar43 = *(undefined4 *)(iVar16 + 500);
  uVar44 = *(undefined4 *)(iVar16 + 0x20c);
  uVar45 = *(undefined4 *)(iVar16 + 0x210);
  uVar46 = *(undefined4 *)(iVar16 + 0x214);
  uVar52 = *(undefined4 *)(iVar16 + 0x218);
  uVar53 = *(undefined4 *)(iVar16 + 0x21c);
  uVar54 = *(undefined4 *)(iVar16 + 0x1f8);
  uVar55 = *(undefined4 *)(iVar16 + 0x1fc);
  uVar56 = *(undefined4 *)(iVar16 + 0x200);
  uVar61 = *(undefined4 *)(iVar16 + 0x204);
  uVar62 = *(undefined4 *)(iVar16 + 0x208);
  uVar63 = *(undefined4 *)(iVar16 + 0x220);
  *(undefined4 *)(puVar7 + 0x7e) = *(undefined4 *)(iVar16 + 0x1e0);
  *(undefined4 *)((int)puVar7 + 0x3f4) = uVar17;
  *(undefined4 *)(puVar7 + 0x7f) = uVar21;
  *(undefined4 *)((int)puVar7 + 0x3fc) = uVar26;
  *(undefined4 *)(puVar7 + 0x80) = uVar28;
  *(undefined4 *)((int)puVar7 + 0x404) = uVar43;
  *(undefined4 *)(puVar7 + 0x7c) = uVar44;
  *(undefined4 *)(puVar7 + 0x86) = uVar45;
  *(undefined4 *)((int)puVar7 + 0x434) = uVar46;
  *(undefined4 *)(puVar7 + 0x87) = uVar52;
  *(undefined4 *)((int)puVar7 + 0x43c) = uVar53;
  uVar29 = uVar13 - 800;
  *(undefined4 *)(puVar7 + 0x81) = uVar54;
  *(undefined4 *)(puVar7 + 0x7a) = uVar55;
  *(undefined4 *)((int)puVar7 + 0x3d4) = uVar56;
  *(undefined4 *)(puVar7 + 0x7b) = uVar61;
  *(undefined4 *)((int)puVar7 + 0x3dc) = uVar62;
  *(undefined4 *)(puVar7 + 0x88) = uVar63;
  uVar17 = *(undefined4 *)(iVar16 + 0x228);
  iVar20 = 0;
  uVar21 = *(undefined4 *)(iVar16 + 0x22c);
  uVar26 = *(undefined4 *)(iVar16 + 0x230);
  uVar28 = *(undefined4 *)(iVar16 + 0x234);
  *(undefined4 *)(puVar7 + 0x76) = *(undefined4 *)(iVar16 + 0x224);
  puVar7[0xc0] = uVar29;
  *(undefined4 *)((int)puVar7 + 0x3b4) = uVar17;
  *(undefined4 *)(puVar7 + 0x77) = uVar21;
  *(undefined4 *)((int)puVar7 + 0x3bc) = uVar26;
  *(undefined4 *)(puVar7 + 0x78) = uVar28;
  puVar34 = (undefined4 *)(iVar15 + 0x618);
  while( true ) {
    puVar19 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),
                                        *(undefined4 *)(iVar18 + (int)uVar29));
    iVar27 = iVar15 + iVar18;
    *puVar34 = puVar19;
    if ((puVar19 != (uint *)0x0) && ((*puVar19 & 0x400000) != 0)) {
      *puVar19 = *puVar19 & 0xffbfffff | 0x40000000;
    }
    if (iVar20 < 5) {
      uVar17 = *(undefined4 *)(iVar15 + 0x60);
      *(undefined4 *)(iVar27 + 0xf2c) = *(undefined4 *)((int)local_340 + iVar18);
      uVar17 = _opd_FUN_00242bf0(uVar17,*(undefined4 *)(local_2e0 + iVar18));
      *(undefined4 *)(iVar27 + 0xf40) = uVar17;
      uVar17 = _opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),
                                 *(undefined4 *)((int)local_360 + iVar18));
      *(undefined4 *)(iVar27 + 0xf54) = uVar17;
    }
    if (iVar20 == 6) break;
    iVar20 = iVar20 + 1;
    iVar18 = iVar18 + 4;
    uVar29 = puVar7[0xc0];
    puVar34 = puVar34 + 5;
  }
  uVar17 = *(undefined4 *)(iVar16 + 0x238);
  uVar21 = *(undefined4 *)(iVar16 + 0x23c);
  uVar26 = *(undefined4 *)(iVar16 + 0x244);
  uVar28 = *(undefined4 *)(iVar16 + 0x248);
  uVar43 = *(undefined4 *)(iVar16 + 0x24c);
  uVar44 = *(undefined4 *)(iVar16 + 0x250);
  uVar45 = *(undefined4 *)(iVar16 + 0x254);
  *(undefined4 *)(puVar7 + 0x7f) = *(undefined4 *)(iVar16 + 0x240);
  *(undefined4 *)(puVar7 + 0x7e) = uVar17;
  *(undefined4 *)((int)puVar7 + 0x3f4) = uVar21;
  *(undefined4 *)((int)puVar7 + 0x3fc) = uVar26;
  *(undefined4 *)(puVar7 + 0x80) = uVar28;
  *(undefined4 *)((int)puVar7 + 0x404) = uVar43;
  *(undefined4 *)(puVar7 + 0x81) = uVar44;
  *(undefined4 *)((int)puVar7 + 0x40c) = uVar45;
  puVar19 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),uVar17);
  *(uint **)(iVar15 + 0x6a8) = puVar19;
  if ((puVar19 != (uint *)0x0) && ((*puVar19 & 0x400000) != 0)) {
    *puVar19 = *puVar19 & 0xffbfffff | 0x40000000;
  }
  puVar19 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),
                                      *(undefined4 *)((int)puVar7 + 0x3f4));
  *(uint **)(iVar15 + 0x6bc) = puVar19;
  if ((puVar19 != (uint *)0x0) && ((*puVar19 & 0x400000) != 0)) {
    *puVar19 = *puVar19 & 0xffbfffff | 0x40000000;
  }
  puVar19 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),*(undefined4 *)(puVar7 + 0x7f))
  ;
  *(uint **)(iVar15 + 0x6d0) = puVar19;
  if ((puVar19 != (uint *)0x0) && ((*puVar19 & 0x400000) != 0)) {
    *puVar19 = *puVar19 & 0xffbfffff | 0x40000000;
  }
  puVar19 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),
                                      *(undefined4 *)((int)puVar7 + 0x3fc));
  *(uint **)(iVar15 + 0x6e4) = puVar19;
  if ((puVar19 != (uint *)0x0) && ((*puVar19 & 0x400000) != 0)) {
    *puVar19 = *puVar19 & 0xffbfffff | 0x40000000;
  }
  puVar19 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),*(undefined4 *)(puVar7 + 0x80))
  ;
  *(uint **)(iVar15 + 0x6f8) = puVar19;
  if ((puVar19 != (uint *)0x0) && ((*puVar19 & 0x400000) != 0)) {
    *puVar19 = *puVar19 & 0xffbfffff | 0x40000000;
  }
  puVar19 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),
                                      *(undefined4 *)((int)puVar7 + 0x404));
  *(uint **)(iVar15 + 0x70c) = puVar19;
  if ((puVar19 != (uint *)0x0) && ((*puVar19 & 0x400000) != 0)) {
    *puVar19 = *puVar19 & 0xffbfffff | 0x40000000;
  }
  puVar19 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),*(undefined4 *)(puVar7 + 0x81))
  ;
  *(uint **)(iVar15 + 0x720) = puVar19;
  if ((puVar19 != (uint *)0x0) && ((*puVar19 & 0x400000) != 0)) {
    *puVar19 = *puVar19 & 0xffbfffff | 0x40000000;
  }
  puVar19 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),
                                      *(undefined4 *)((int)puVar7 + 0x40c));
  *(uint **)(iVar15 + 0x734) = puVar19;
  if ((puVar19 != (uint *)0x0) && ((*puVar19 & 0x400000) != 0)) {
    *puVar19 = *puVar19 & 0xffbfffff | 0x40000000;
  }
  if (*(int *)(iVar15 + 0x6c) != 0) {
    iVar20 = 0;
    _opd_FUN_00f9ac38(uVar31,iVar16 + 600,0x60);
    puVar34 = (undefined4 *)(iVar15 + 0x950);
    do {
      puVar19 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x6c),
                                          *(undefined4 *)(iVar20 + iVar32));
      bVar1 = iVar20 != 0x5c;
      *puVar34 = puVar19;
      if ((puVar19 != (uint *)0x0) && ((*puVar19 & 0x400000) != 0)) {
        *puVar19 = *puVar19 & 0xffbfffff | 0x40000000;
      }
      iVar20 = iVar20 + 4;
      puVar34 = puVar34 + 5;
    } while (bVar1);
  }
  uVar17 = *(undefined4 *)(iVar16 + 0x2c0);
  uVar21 = *(undefined4 *)(iVar16 + 0x2c4);
  uVar26 = *(undefined4 *)(iVar16 + 0x2c8);
  uVar28 = *(undefined4 *)(iVar16 + 0x2cc);
  uVar43 = *(undefined4 *)(iVar16 + 0x2d0);
  uVar44 = *(undefined4 *)(iVar16 + 0x2d4);
  uVar45 = *(undefined4 *)(iVar16 + 0x2b8);
  *(undefined4 *)((int)puVar7 + 0x3f4) = *(undefined4 *)(iVar16 + 700);
  *(undefined4 *)(puVar7 + 0x7f) = uVar17;
  *(undefined4 *)((int)puVar7 + 0x3fc) = uVar21;
  *(undefined4 *)(puVar7 + 0x80) = uVar26;
  *(undefined4 *)((int)puVar7 + 0x404) = uVar28;
  *(undefined4 *)(puVar7 + 0x81) = uVar43;
  *(undefined4 *)((int)puVar7 + 0x40c) = uVar44;
  *(undefined4 *)(puVar7 + 0x7e) = uVar45;
  _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),uVar45);
  _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),*(undefined4 *)((int)puVar7 + 0x3f4));
  _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),*(undefined4 *)(puVar7 + 0x7f));
  _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),*(undefined4 *)((int)puVar7 + 0x3fc));
  _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),*(undefined4 *)(puVar7 + 0x80));
  _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),*(undefined4 *)((int)puVar7 + 0x404));
  _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),*(undefined4 *)(puVar7 + 0x81));
  _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),*(undefined4 *)((int)puVar7 + 0x40c));
  if (*(int *)(iVar15 + 0x68) != 0) {
    iVar20 = 0;
    _opd_FUN_00f9ac38(uVar31,iVar16 + 0x2d8,0xc0);
    puVar34 = (undefined4 *)(iVar15 + 0xb34);
    do {
      puVar19 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x68),
                                          *(undefined4 *)(iVar20 + iVar32));
      bVar1 = iVar20 != 0xbc;
      *puVar34 = puVar19;
      if ((puVar19 != (uint *)0x0) && ((*puVar19 & 0x400000) != 0)) {
        *puVar19 = *puVar19 & 0xffbfffff | 0x40000000;
      }
      iVar20 = iVar20 + 4;
      puVar34 = puVar34 + 5;
    } while (bVar1);
  }
  puVar19 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),0x6ce5d6);
  *(uint **)(iVar15 + 0xef8) = puVar19;
  if ((puVar19 != (uint *)0x0) && ((*puVar19 & 0x400000) != 0)) {
    *puVar19 = *puVar19 & 0xffbfffff | 0x40000000;
  }
  puVar19 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),0xb3b305);
  *(uint **)(iVar15 + 0xf14) = puVar19;
  if ((puVar19 != (uint *)0x0) && ((*puVar19 & 0x400000) != 0)) {
    *puVar19 = *puVar19 & 0xffbfffff | 0x40000000;
  }
  iVar16 = _opd_FUN_00b18730();
  if (iVar16 == 0) {
    _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),&DAT_009c443f);
    _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),&DAT_009a335f);
  }
  else {
    _opd_FUN_00b30880(*(undefined4 *)(iVar15 + 0x60),&DAT_009c443f);
    _opd_FUN_00b30880(*(undefined4 *)(iVar15 + 0x60),&DAT_009a335f);
    uVar17 = *(undefined4 *)(iVar15 + 0x60);
    uVar21 = _opd_FUN_00242bf0(uVar17,&DAT_009a335f);
    _opd_FUN_00b04938(uVar17,0x516054,uVar21);
    uVar17 = *(undefined4 *)(iVar15 + 0x60);
    uVar21 = _opd_FUN_00242bf0(uVar17,&DAT_009c443f);
    _opd_FUN_00b04938(uVar17,0x516055,uVar21);
  }
  lVar30 = uVar13 - 0x440;
  _opd_FUN_00b19208(*(undefined4 *)(iVar15 + 0x60),0x654136,0);
  _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0x294833);
  _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0x1857f3);
  _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0x5feb2);
  _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0xbfef94);
  _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0x1d3c1c);
  _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0x8619b7);
  _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0x20d2b7);
  _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0x5fd273);
  _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0xface29);
  _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0xbe5911);
  _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0xf07686);
  _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0x66e42a);
  uVar17 = *(undefined4 *)(puVar10 + -0x7fa8);
  _opd_FUN_00b310a0(*(undefined4 *)(iVar15 + 0x60),0xface29,uVar17,0);
  _opd_FUN_00b310a0(*(undefined4 *)(iVar15 + 0x60),0xbe5911,uVar17,0);
  _opd_FUN_00b310a0(*(undefined4 *)(iVar15 + 0x60),0xf07686,*(undefined4 *)(puVar10 + -0x7fa4),0);
  _opd_FUN_00b310a0(*(undefined4 *)(iVar15 + 0x60),0x66e42a,*(undefined4 *)(puVar10 + -0x7fa0),0);
  _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0x6892eb);
  _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0xedeeea);
  _opd_FUN_0023ca38(*(undefined4 *)(iVar15 + 0x60));
  _opd_FUN_00bd3f30(iVar15);
  uVar17 = _opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),0x5e24f3);
  _opd_FUN_00253a08(uVar17,lVar30);
  *(int *)(iVar15 + 0xfc8) = (int)*(short *)(puVar7 + 0x5a);
  iVar16 = _opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),0x5e24f3);
  _opd_FUN_00253de0(iVar16,iVar15 + 0xfcc,iVar15 + 0xfce,0,0);
  uVar12 = 0xffff;
  if (iVar16 != 0) {
    uVar12 = *(ushort *)(iVar16 + 0x1a) & 0xfff;
  }
  *(ushort *)(iVar15 + 0xfd0) = uVar12;
  *(undefined8 *)(iVar15 + 0xfd8) = *(undefined8 *)(iVar15 + 0xfc8);
  *(undefined8 *)(iVar15 + 0xfe0) = *(undefined8 *)(iVar15 + 0xfd0);
  _opd_FUN_00253de0(*(undefined4 *)(iVar15 + 0x74),iVar15 + 0xfec,iVar15 + 0xfee,0,0);
  uVar12 = 0xffff;
  if (*(int *)(iVar15 + 0x74) != 0) {
    uVar12 = *(ushort *)(*(int *)(iVar15 + 0x74) + 0x1a) & 0xfff;
  }
  *(ushort *)(iVar15 + 0xff0) = uVar12;
  *(undefined8 *)(iVar15 + 0x1000) = *(undefined8 *)(iVar15 + 0xff0);
  *(undefined8 *)(iVar15 + 0xff8) = *(undefined8 *)(iVar15 + 0xfe8);
  uVar17 = _opd_FUN_00036350();
  puVar22 = (undefined *)_opd_FUN_000cdc90(uVar17);
  puVar19 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),0x2f66f6);
  puVar23 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),0x2f66f7);
  puVar24 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),0x2f66f8);
  puVar25 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),0x2f66f9);
  if ((puVar19 != (uint *)0x0) && ((*puVar19 & 0x400000) == 0)) {
    *puVar19 = *puVar19 | 0x40400000;
  }
  if ((puVar23 != (uint *)0x0) && ((*puVar23 & 0x400000) != 0)) {
    *puVar23 = *puVar23 & 0xffbfffff | 0x40000000;
  }
  if ((puVar24 != (uint *)0x0) && ((*puVar24 & 0x400000) != 0)) {
    *puVar24 = *puVar24 & 0xffbfffff | 0x40000000;
  }
  *(undefined2 *)(iVar15 + 0xfd6) = 0;
  *(undefined2 *)(iVar15 + 0xfd4) = 1;
  *(undefined2 *)(iVar15 + 0xfd2) = 0;
  if ((puVar25 != (uint *)0x0) && ((*puVar25 & 0x400000) == 0)) {
    *puVar25 = *puVar25 | 0x40400000;
  }
  if (puVar22 == &UNK_00f8c727) {
    *(undefined2 *)(iVar15 + 0xfd4) = 3;
    _opd_FUN_00b30880(*(undefined4 *)(iVar15 + 0x60),0x9451a5);
    _opd_FUN_00b30880(*(undefined4 *)(iVar15 + 0x60),0x9451a4);
    _opd_FUN_00b30880(*(undefined4 *)(iVar15 + 0x60),0x9451a3);
    *(undefined2 *)(iVar15 + 0xfd6) = 1;
    *(undefined2 *)(iVar15 + 0xfd2) = 1;
    _opd_FUN_00253be0(puVar19,0xfffffffffffff000,0xfffffffffffff000,0,0);
    _opd_FUN_00253be0(puVar23,0xfffffffffffff000,0xfffffffffffff000,0,0);
    _opd_FUN_00253be0(puVar24,0xfffffffffffff000,0xfffffffffffff000,0,0);
    _opd_FUN_00253be0(puVar25,0xfffffffffffff000,0xfffffffffffff000,0,0);
    uVar17 = *(undefined4 *)(puVar10 + -0x7f98);
    puVar34 = (undefined4 *)(*(uint *)(puVar10 + -0x7f9c) & 0xfffffff0);
    uVar21 = *puVar34;
    uVar26 = puVar34[1];
    uVar28 = puVar34[2];
    uVar43 = puVar34[3];
    puVar34 = (undefined4 *)(uVar13 - 0x510);
    *puVar34 = uVar21;
    puVar34[1] = uVar26;
    puVar34[2] = uVar28;
    puVar34[3] = uVar43;
    puVar34 = (undefined4 *)(uVar13 - 0x520);
    *puVar34 = uVar21;
    puVar34[1] = uVar26;
    puVar34[2] = uVar28;
    puVar34[3] = uVar43;
    puVar34 = (undefined4 *)(uVar13 - 0x580);
    *puVar34 = uVar21;
    puVar34[1] = uVar26;
    puVar34[2] = uVar28;
    puVar34[3] = uVar43;
    uVar17 = _opd_FUN_000cdc90(uVar17);
    _opd_FUN_001a6020(uVar13 - 0x510 & 0xffffffff,uVar17);
    uVar17 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f94));
    _opd_FUN_001a6020(uVar13 - 0x520 & 0xffffffff,uVar17);
    uVar17 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f90));
    _opd_FUN_001a6020(uVar13 - 0x580 & 0xffffffff,uVar17);
    uVar26 = *(undefined4 *)(puVar10 + -0x7f6c);
    uVar17 = *(undefined4 *)(puVar10 + -0x7dfc);
    uVar21 = *(undefined4 *)(puVar10 + -0x7e4c);
    uVar28 = *(undefined4 *)(iVar15 + 0x60);
    *(float *)(iVar15 + 0xfb8) = -*(float *)(puVar7 + 0x32);
    fVar2 = *(float *)(puVar7 + 0x33);
    *(undefined4 *)(iVar15 + 0xfb8) = *(undefined4 *)(puVar10 + -0x7df4);
    *(undefined4 *)(iVar15 + 0xfa8) = uVar17;
    *(undefined4 *)(iVar15 + 0xfac) = uVar21;
    *(float *)(iVar15 + 0xfbc) = -fVar2;
    *(undefined4 *)(iVar15 + 0xfbc) = *(undefined4 *)(puVar10 + -0x7dec);
    uVar17 = _opd_FUN_000cdc90(uVar26);
    uVar21 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7de4));
    _opd_FUN_00b30be8(uVar28,0x2f66f6,uVar17,uVar21);
    uVar17 = *(undefined4 *)(iVar15 + 0x60);
    uVar21 = _opd_FUN_000cdc90(uVar26);
    uVar28 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7de0));
    _opd_FUN_00b30be8(uVar17,0x2f66f7,uVar21,uVar28);
    uVar17 = *(undefined4 *)(iVar15 + 0x60);
    uVar21 = _opd_FUN_000cdc90(uVar26);
    uVar28 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7ddc));
    _opd_FUN_00b30be8(uVar17,0x2f66f8,uVar21,uVar28);
    uVar17 = *(undefined4 *)(iVar15 + 0x60);
    uVar21 = _opd_FUN_000cdc90(uVar26);
    uVar26 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7dd8));
    uVar35 = 0x2000;
    uVar33 = 0x2000;
    _opd_FUN_00b30be8(uVar17,0x2f66f9,uVar21,uVar26);
    goto LAB_00bd99d0;
  }
  if ((int)puVar22 < 0xf8c728) {
    if (puVar22 == &UNK_00f8c687) {
      *(undefined2 *)(iVar15 + 0xfd4) = 1;
      _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0x9451a5);
      _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0x9451a4);
      _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0x9451a3);
      _opd_FUN_00253be0(puVar19,0xfffffffffffff000,0xfffffffffffff000,0,0);
      _opd_FUN_00253be0(puVar25,0xfffffffffffff000,0xfffffffffffff000,0,0);
      uVar17 = *(undefined4 *)(puVar10 + -0x7f98);
      puVar34 = (undefined4 *)(*(uint *)(puVar10 + -0x7f9c) & 0xfffffff0);
      uVar21 = *puVar34;
      uVar26 = puVar34[1];
      uVar28 = puVar34[2];
      uVar43 = puVar34[3];
      puVar34 = (undefined4 *)(uVar13 - 0x3f0);
      *puVar34 = uVar21;
      puVar34[1] = uVar26;
      puVar34[2] = uVar28;
      puVar34[3] = uVar43;
      puVar34 = (undefined4 *)(uVar13 - 0x480);
      *puVar34 = uVar21;
      puVar34[1] = uVar26;
      puVar34[2] = uVar28;
      puVar34[3] = uVar43;
      puVar34 = (undefined4 *)(uVar13 - 0x490);
      *puVar34 = uVar21;
      puVar34[1] = uVar26;
      puVar34[2] = uVar28;
      puVar34[3] = uVar43;
      uVar17 = _opd_FUN_000cdc90(uVar17);
      _opd_FUN_001a6020(uVar13 - 0x3f0 & 0xffffffff,uVar17);
      uVar17 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f94));
      _opd_FUN_001a6020(uVar13 - 0x480 & 0xffffffff,uVar17);
      uVar17 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f90));
      _opd_FUN_001a6020(uVar13 - 0x490 & 0xffffffff,uVar17);
      uVar26 = *(undefined4 *)(puVar10 + -0x7f6c);
      uVar17 = *(undefined4 *)(puVar10 + -0x7f04);
      uVar21 = *(undefined4 *)(puVar10 + -0x7f24);
      uVar28 = *(undefined4 *)(iVar15 + 0x60);
      *(float *)(iVar15 + 0xfb8) = -*(float *)(puVar7 + 0x50);
      fVar2 = *(float *)(puVar7 + 0x51);
      *(undefined4 *)(iVar15 + 0xfb8) = 0x43fa0000;
      *(undefined4 *)(iVar15 + 0xfa8) = uVar17;
      *(undefined4 *)(iVar15 + 0xfac) = uVar21;
      *(float *)(iVar15 + 0xfbc) = -fVar2;
      *(undefined4 *)(iVar15 + 0xfbc) = *(undefined4 *)(puVar10 + -0x7efc);
      uVar17 = _opd_FUN_000cdc90(uVar26);
      uVar21 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7ef4));
      _opd_FUN_00b30be8(uVar28,0x2f66f6,uVar17,uVar21);
      uVar17 = *(undefined4 *)(iVar15 + 0x60);
      uVar26 = _opd_FUN_000cdc90(uVar26);
      uVar21 = *(undefined4 *)(puVar10 + -0x7ef0);
    }
    else if ((int)puVar22 < 0xf8c688) {
      if (puVar22 == (undefined *)0x494ec7) {
LAB_00bdab34:
        *(undefined2 *)(iVar15 + 0xfd4) = 3;
        _opd_FUN_00b30880(*(undefined4 *)(iVar15 + 0x60),0x9451a5);
        _opd_FUN_00b30880(*(undefined4 *)(iVar15 + 0x60),0x9451a4);
        _opd_FUN_00b30880(*(undefined4 *)(iVar15 + 0x60),0x9451a3);
        _opd_FUN_00253be0(puVar19,0xfffffffffffff000,0xfffffffffffff000,0,0);
        _opd_FUN_00253be0(puVar23,0xfffffffffffff000,0xfffffffffffff000,0,0);
        _opd_FUN_00253be0(puVar24,0xfffffffffffff000,0xfffffffffffff000,0,0);
        _opd_FUN_00253be0(puVar25,0xfffffffffffff000,0xfffffffffffff000,0,0);
        uVar17 = *(undefined4 *)(puVar10 + -0x7f98);
        puVar34 = (undefined4 *)(*(uint *)(puVar10 + -0x7f9c) & 0xfffffff0);
        uVar21 = *puVar34;
        uVar26 = puVar34[1];
        uVar28 = puVar34[2];
        uVar43 = puVar34[3];
        puVar34 = (undefined4 *)(uVar13 - 0x380);
        *puVar34 = uVar21;
        puVar34[1] = uVar26;
        puVar34[2] = uVar28;
        puVar34[3] = uVar43;
        puVar34 = (undefined4 *)(uVar13 - 0x370);
        *puVar34 = uVar21;
        puVar34[1] = uVar26;
        puVar34[2] = uVar28;
        puVar34[3] = uVar43;
        puVar34 = (undefined4 *)(uVar13 - 0x3b0);
        *puVar34 = uVar21;
        puVar34[1] = uVar26;
        puVar34[2] = uVar28;
        puVar34[3] = uVar43;
        uVar17 = _opd_FUN_000cdc90(uVar17);
        _opd_FUN_001a6020(uVar13 - 0x380 & 0xffffffff,uVar17);
        uVar17 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f94));
        _opd_FUN_001a6020(uVar13 - 0x370 & 0xffffffff,uVar17);
        uVar17 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f90));
        _opd_FUN_001a6020(uVar13 - 0x3b0 & 0xffffffff,uVar17);
        uVar17 = *(undefined4 *)(puVar10 + -0x7f60);
        uVar21 = *(undefined4 *)(puVar10 + -0x7f50);
        *(float *)(iVar15 + 0xfb8) = -*(float *)(puVar7 + 0x6c);
        fVar2 = *(float *)(puVar7 + 0x6d);
        *(undefined4 *)(iVar15 + 0xfa8) = uVar17;
        *(undefined4 *)(iVar15 + 0xfac) = uVar17;
        *(float *)(iVar15 + 0xfbc) = -fVar2;
        *(undefined4 *)(iVar15 + 0xfb8) = *(undefined4 *)(puVar10 + -0x7f58);
        *(undefined4 *)(iVar15 + 0xfbc) = uVar21;
        iVar16 = _opd_FUN_00b19a70();
        if (iVar16 == 0) {
          uVar26 = *(undefined4 *)(puVar10 + -0x7f6c);
          uVar17 = *(undefined4 *)(iVar15 + 0x60);
          uVar21 = _opd_FUN_000cdc90(uVar26);
          uVar28 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f3c));
          _opd_FUN_00b30be8(uVar17,0x2f66f6,uVar21,uVar28);
          uVar17 = *(undefined4 *)(iVar15 + 0x60);
          uVar21 = _opd_FUN_000cdc90(uVar26);
          uVar28 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f38));
          _opd_FUN_00b30be8(uVar17,0x2f66f7,uVar21,uVar28);
          uVar21 = *(undefined4 *)(iVar15 + 0x60);
          uVar28 = _opd_FUN_000cdc90(uVar26);
          uVar17 = *(undefined4 *)(puVar10 + -0x7f34);
        }
        else {
          uVar26 = *(undefined4 *)(puVar10 + -0x7f6c);
          uVar17 = *(undefined4 *)(iVar15 + 0x60);
          uVar21 = _opd_FUN_000cdc90(uVar26);
          uVar28 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f48));
          _opd_FUN_00b30be8(uVar17,0x2f66f6,uVar21,uVar28);
          uVar17 = *(undefined4 *)(iVar15 + 0x60);
          uVar21 = _opd_FUN_000cdc90(uVar26);
          uVar28 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f44));
          _opd_FUN_00b30be8(uVar17,0x2f66f7,uVar21,uVar28);
          uVar21 = *(undefined4 *)(iVar15 + 0x60);
          uVar28 = _opd_FUN_000cdc90(uVar26);
          uVar17 = *(undefined4 *)(puVar10 + -0x7f40);
        }
        uVar17 = _opd_FUN_000cdc90(uVar17);
        _opd_FUN_00b30be8(uVar21,0x2f66f8,uVar28,uVar17);
        uVar17 = *(undefined4 *)(iVar15 + 0x60);
        uVar26 = _opd_FUN_000cdc90(uVar26);
        uVar21 = *(undefined4 *)(puVar10 + -0x7f30);
      }
      else {
        if (puVar22 != (undefined *)0x494f07) {
          if (puVar22 != (undefined *)0x494b28) goto LAB_00bd98b0;
LAB_00bd9680:
          _opd_FUN_00253be0(puVar19,0xffffffffffffed2d,0xffffffffffffed2d,0,0);
          _opd_FUN_00253be0(puVar25,0xffffffffffffed2d,0xffffffffffffed2d,0,0);
          *(undefined2 *)(iVar15 + 0xfd4) = 1;
          _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0x9451a5);
          _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0x9451a4);
          _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0x9451a3);
          uVar17 = *(undefined4 *)(puVar10 + -0x7f98);
          puVar34 = (undefined4 *)(*(uint *)(puVar10 + -0x7f9c) & 0xfffffff0);
          uVar21 = *puVar34;
          uVar26 = puVar34[1];
          uVar28 = puVar34[2];
          uVar43 = puVar34[3];
          puVar34 = (undefined4 *)(uVar13 - 0x410);
          *puVar34 = uVar21;
          puVar34[1] = uVar26;
          puVar34[2] = uVar28;
          puVar34[3] = uVar43;
          puVar34 = (undefined4 *)(uVar13 - 0x400);
          *puVar34 = uVar21;
          puVar34[1] = uVar26;
          puVar34[2] = uVar28;
          puVar34[3] = uVar43;
          puVar34 = (undefined4 *)(uVar13 - 0x3a0);
          *puVar34 = uVar21;
          puVar34[1] = uVar26;
          puVar34[2] = uVar28;
          puVar34[3] = uVar43;
          uVar17 = _opd_FUN_000cdc90(uVar17);
          _opd_FUN_001a6020(uVar13 - 0x410 & 0xffffffff,uVar17);
          uVar17 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f94));
          _opd_FUN_001a6020(uVar13 - 0x400 & 0xffffffff,uVar17);
          uVar17 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f90));
          _opd_FUN_001a6020(uVar13 - 0x3a0 & 0xffffffff,uVar17);
          uVar26 = *(undefined4 *)(puVar10 + -0x7f6c);
          uVar17 = *(undefined4 *)(puVar10 + -0x7f8c);
          uVar21 = *(undefined4 *)(puVar10 + -0x7f84);
          uVar28 = *(undefined4 *)(iVar15 + 0x60);
          *(float *)(iVar15 + 0xfb8) = -*(float *)(puVar7 + 0x6e);
          fVar2 = *(float *)(puVar7 + 0x6f);
          *(undefined4 *)(iVar15 + 0xfb8) = *(undefined4 *)(puVar10 + -0x7f7c);
          *(undefined4 *)(iVar15 + 0xfa8) = uVar17;
          *(undefined4 *)(iVar15 + 0xfac) = uVar21;
          *(float *)(iVar15 + 0xfbc) = -fVar2;
          *(undefined4 *)(iVar15 + 0xfbc) = *(undefined4 *)(puVar10 + -0x7f74);
          uVar17 = _opd_FUN_000cdc90(uVar26);
          uVar21 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f68));
          _opd_FUN_00b30be8(uVar28,0x2f66f6,uVar17,uVar21);
          uVar17 = *(undefined4 *)(iVar15 + 0x60);
          uVar21 = _opd_FUN_000cdc90(uVar26);
          uVar26 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f64));
          uVar35 = 0x25a6;
          uVar33 = 0x25a6;
          _opd_FUN_00b30be8(uVar17,0x2f66f9,uVar21,uVar26);
          goto LAB_00bd99d0;
        }
LAB_00bdb250:
        *(undefined2 *)(iVar15 + 0xfd4) = 3;
        _opd_FUN_00b30880(*(undefined4 *)(iVar15 + 0x60),0x9451a5);
        _opd_FUN_00b30880(*(undefined4 *)(iVar15 + 0x60),0x9451a4);
        _opd_FUN_00b30880(*(undefined4 *)(iVar15 + 0x60),0x9451a3);
        _opd_FUN_00253be0(puVar19,0xfffffffffffff000,0xfffffffffffff000,0,0);
        _opd_FUN_00253be0(puVar23,0xfffffffffffff000,0xfffffffffffff000,0,0);
        _opd_FUN_00253be0(puVar24,0xfffffffffffff000,0xfffffffffffff000,0,0);
        _opd_FUN_00253be0(puVar25,0xfffffffffffff000,0xfffffffffffff000,0,0);
        uVar17 = *(undefined4 *)(puVar10 + -0x7f98);
        puVar34 = (undefined4 *)(*(uint *)(puVar10 + -0x7f9c) & 0xfffffff0);
        uVar21 = *puVar34;
        uVar26 = puVar34[1];
        uVar28 = puVar34[2];
        uVar43 = puVar34[3];
        puVar34 = (undefined4 *)(uVar13 - 0x3c0);
        *puVar34 = uVar21;
        puVar34[1] = uVar26;
        puVar34[2] = uVar28;
        puVar34[3] = uVar43;
        puVar34 = (undefined4 *)(uVar13 - 0x3d0);
        *puVar34 = uVar21;
        puVar34[1] = uVar26;
        puVar34[2] = uVar28;
        puVar34[3] = uVar43;
        puVar34 = (undefined4 *)(uVar13 - 0x650);
        *puVar34 = uVar21;
        puVar34[1] = uVar26;
        puVar34[2] = uVar28;
        puVar34[3] = uVar43;
        uVar17 = _opd_FUN_000cdc90(uVar17);
        _opd_FUN_001a6020(uVar13 - 0x3c0 & 0xffffffff,uVar17);
        uVar17 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f94));
        _opd_FUN_001a6020(uVar13 - 0x3d0 & 0xffffffff,uVar17);
        uVar17 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f90));
        _opd_FUN_001a6020(uVar13 - 0x650 & 0xffffffff,uVar17);
        uVar28 = *(undefined4 *)(puVar10 + -0x7f6c);
        uVar17 = *(undefined4 *)(puVar10 + -0x7f1c);
        uVar21 = *(undefined4 *)(puVar10 + -0x7f2c);
        uVar26 = *(undefined4 *)(puVar10 + -0x7f24);
        uVar43 = *(undefined4 *)(iVar15 + 0x60);
        *(float *)(iVar15 + 0xfb8) = -*(float *)(puVar7 + 0x18);
        fVar2 = *(float *)(puVar7 + 0x19);
        *(undefined2 *)(iVar15 + 0xfd6) = 1;
        *(undefined4 *)(iVar15 + 0xfb8) = uVar17;
        *(undefined2 *)(iVar15 + 0xfd2) = 1;
        *(undefined4 *)(iVar15 + 0xfa8) = uVar21;
        *(undefined4 *)(iVar15 + 0xfac) = uVar26;
        *(float *)(iVar15 + 0xfbc) = -fVar2;
        *(undefined4 *)(iVar15 + 0xfbc) = uVar17;
        uVar17 = _opd_FUN_000cdc90(uVar28);
        uVar21 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f14));
        _opd_FUN_00b30be8(uVar43,0x2f66f6,uVar17,uVar21);
        uVar17 = *(undefined4 *)(iVar15 + 0x60);
        uVar21 = _opd_FUN_000cdc90(uVar28);
        uVar26 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f10));
        _opd_FUN_00b30be8(uVar17,0x2f66f7,uVar21,uVar26);
        uVar17 = *(undefined4 *)(iVar15 + 0x60);
        uVar21 = _opd_FUN_000cdc90(uVar28);
        uVar26 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f0c));
        _opd_FUN_00b30be8(uVar17,0x2f66f8,uVar21,uVar26);
        uVar17 = *(undefined4 *)(iVar15 + 0x60);
        uVar26 = _opd_FUN_000cdc90(uVar28);
        uVar21 = *(undefined4 *)(puVar10 + -0x7f08);
      }
    }
    else {
      if (puVar22 != &UNK_00f8c6c7) {
        if ((int)puVar22 < 0xf8c6c8) {
          if (puVar22 == &UNK_00f8c6a7) goto LAB_00bd9680;
        }
        else {
          if (puVar22 == &UNK_00f8c6e7) goto LAB_00bdab34;
          if (puVar22 == &UNK_00f8c707) goto LAB_00bdb250;
        }
        goto LAB_00bd98b0;
      }
      *(undefined2 *)(iVar15 + 0xfd4) = 2;
      _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0x9451a5);
      _opd_FUN_00b30880(*(undefined4 *)(iVar15 + 0x60),0x9451a4);
      _opd_FUN_00b30880(*(undefined4 *)(iVar15 + 0x60),0x9451a3);
      _opd_FUN_00253be0(puVar19,0xfffffffffffff000,0xfffffffffffff000,0,0);
      _opd_FUN_00253be0(puVar23,0xfffffffffffff000,0xfffffffffffff000,0,0);
      _opd_FUN_00253be0(puVar25,0xfffffffffffff000,0xfffffffffffff000,0,0);
      uVar17 = *(undefined4 *)(puVar10 + -0x7f98);
      puVar34 = (undefined4 *)(*(uint *)(puVar10 + -0x7f9c) & 0xfffffff0);
      uVar21 = *puVar34;
      uVar26 = puVar34[1];
      uVar28 = puVar34[2];
      uVar43 = puVar34[3];
      puVar34 = (undefined4 *)(uVar13 - 0x630);
      *puVar34 = uVar21;
      puVar34[1] = uVar26;
      puVar34[2] = uVar28;
      puVar34[3] = uVar43;
      puVar34 = (undefined4 *)(uVar13 - 0x620);
      *puVar34 = uVar21;
      puVar34[1] = uVar26;
      puVar34[2] = uVar28;
      puVar34[3] = uVar43;
      puVar34 = (undefined4 *)(uVar13 - 0x610);
      *puVar34 = uVar21;
      puVar34[1] = uVar26;
      puVar34[2] = uVar28;
      puVar34[3] = uVar43;
      uVar17 = _opd_FUN_000cdc90(uVar17);
      _opd_FUN_001a6020(uVar13 - 0x630 & 0xffffffff,uVar17);
      uVar17 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f94));
      _opd_FUN_001a6020(uVar13 - 0x620 & 0xffffffff,uVar17);
      uVar17 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f90));
      _opd_FUN_001a6020(uVar13 - 0x610 & 0xffffffff,uVar17);
      uVar26 = *(undefined4 *)(puVar10 + -0x7f6c);
      uVar17 = *(undefined4 *)(puVar10 + -0x7eec);
      uVar21 = *(undefined4 *)(puVar10 + -0x7edc);
      uVar28 = *(undefined4 *)(iVar15 + 0x60);
      *(float *)(iVar15 + 0xfb8) = -*(float *)(puVar7 + 0x20);
      fVar2 = *(float *)(puVar7 + 0x21);
      *(undefined2 *)(iVar15 + 0xfd6) = 1;
      *(undefined2 *)(iVar15 + 0xfd2) = 1;
      *(undefined4 *)(iVar15 + 0xfa8) = uVar17;
      *(undefined4 *)(iVar15 + 0xfac) = uVar17;
      *(float *)(iVar15 + 0xfbc) = -fVar2;
      *(undefined4 *)(iVar15 + 0xfb8) = *(undefined4 *)(puVar10 + -0x7ee4);
      *(undefined4 *)(iVar15 + 0xfbc) = uVar21;
      uVar17 = _opd_FUN_000cdc90(uVar26);
      uVar21 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7ed4));
      _opd_FUN_00b30be8(uVar28,0x2f66f6,uVar17,uVar21);
      uVar17 = *(undefined4 *)(iVar15 + 0x60);
      uVar21 = _opd_FUN_000cdc90(uVar26);
      uVar28 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7ed0));
      _opd_FUN_00b30be8(uVar17,0x2f66f7,uVar21,uVar28);
      uVar17 = *(undefined4 *)(iVar15 + 0x60);
      uVar26 = _opd_FUN_000cdc90(uVar26);
      uVar21 = *(undefined4 *)(puVar10 + -0x7ecc);
    }
  }
  else {
    if (puVar22 == &UNK_00f8caa7) {
      *(undefined2 *)(iVar15 + 0xfd4) = 1;
      _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0x9451a5);
      _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0x9451a4);
      _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0x9451a3);
      _opd_FUN_00253be0(puVar19,0xffffffffffffe000,0xfffffffffffff000,0,0);
      _opd_FUN_00253be0(puVar25,0xffffffffffffe000,0xfffffffffffff000,0,0);
      uVar17 = *(undefined4 *)(puVar10 + -0x7f98);
      puVar34 = (undefined4 *)(*(uint *)(puVar10 + -0x7f9c) & 0xfffffff0);
      uVar21 = *puVar34;
      uVar26 = puVar34[1];
      uVar28 = puVar34[2];
      uVar43 = puVar34[3];
      puVar34 = (undefined4 *)(uVar13 - 0x5d0);
      *puVar34 = uVar21;
      puVar34[1] = uVar26;
      puVar34[2] = uVar28;
      puVar34[3] = uVar43;
      puVar34 = (undefined4 *)(uVar13 - 0x5c0);
      *puVar34 = uVar21;
      puVar34[1] = uVar26;
      puVar34[2] = uVar28;
      puVar34[3] = uVar43;
      puVar34 = (undefined4 *)(uVar13 - 0x640);
      *puVar34 = uVar21;
      puVar34[1] = uVar26;
      puVar34[2] = uVar28;
      puVar34[3] = uVar43;
      uVar17 = _opd_FUN_000cdc90(uVar17);
      _opd_FUN_001a6020(uVar13 - 0x5d0 & 0xffffffff,uVar17);
      uVar17 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f94));
      _opd_FUN_001a6020(uVar13 - 0x5c0 & 0xffffffff,uVar17);
      uVar17 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f90));
      _opd_FUN_001a6020(uVar13 - 0x640 & 0xffffffff,uVar17);
      uVar21 = *(undefined4 *)(puVar10 + -0x7f6c);
      uVar17 = *(undefined4 *)(puVar10 + -0x7eb4);
      uVar26 = *(undefined4 *)(iVar15 + 0x60);
      *(float *)(iVar15 + 0xfb8) = -*(float *)(puVar7 + 0x1a);
      fVar2 = *(float *)(puVar7 + 0x1b);
      *(undefined4 *)(iVar15 + 0xfb8) = *(undefined4 *)(puVar10 + -0x7eac);
      *(undefined4 *)(iVar15 + 0xfac) = uVar17;
      *(undefined4 *)(iVar15 + 0xfa8) = uVar17;
      *(float *)(iVar15 + 0xfbc) = -fVar2;
      *(undefined4 *)(iVar15 + 0xfbc) = *(undefined4 *)(puVar10 + -0x7ea4);
      uVar17 = _opd_FUN_000cdc90(uVar21);
      uVar28 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7e9c));
      _opd_FUN_00b30be8(uVar26,0x2f66f6,uVar17,uVar28);
      uVar17 = *(undefined4 *)(iVar15 + 0x60);
      uVar21 = _opd_FUN_000cdc90(uVar21);
      uVar26 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7e98));
      uVar35 = 0x4000;
      uVar33 = 0x2000;
      _opd_FUN_00b30be8(uVar17,0x2f66f9,uVar21,uVar26);
      goto LAB_00bd99d0;
    }
    if ((int)puVar22 < 0xf8caa8) {
      if (puVar22 == &UNK_00f8c767) {
        *(undefined2 *)(iVar15 + 0xfd4) = 1;
        _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0x9451a5);
        _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0x9451a4);
        _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0x9451a3);
        _opd_FUN_00253be0(puVar19,0xfffffffffffff000,0xfffffffffffff000,0,0);
        _opd_FUN_00253be0(puVar25,0xfffffffffffff000,0xfffffffffffff000,0,0);
        uVar17 = *(undefined4 *)(puVar10 + -0x7f98);
        puVar34 = (undefined4 *)(*(uint *)(puVar10 + -0x7f9c) & 0xfffffff0);
        uVar21 = *puVar34;
        uVar26 = puVar34[1];
        uVar28 = puVar34[2];
        uVar43 = puVar34[3];
        puVar34 = (undefined4 *)(uVar13 - 0x680);
        *puVar34 = uVar21;
        puVar34[1] = uVar26;
        puVar34[2] = uVar28;
        puVar34[3] = uVar43;
        puVar34 = (undefined4 *)(uVar13 - 0x690);
        *puVar34 = uVar21;
        puVar34[1] = uVar26;
        puVar34[2] = uVar28;
        puVar34[3] = uVar43;
        puVar34 = (undefined4 *)(uVar13 - 0x5b0);
        *puVar34 = uVar21;
        puVar34[1] = uVar26;
        puVar34[2] = uVar28;
        puVar34[3] = uVar43;
        uVar17 = _opd_FUN_000cdc90(uVar17);
        _opd_FUN_001a6020(uVar13 - 0x680 & 0xffffffff,uVar17);
        uVar17 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f94));
        _opd_FUN_001a6020(uVar13 - 0x690 & 0xffffffff,uVar17);
        uVar17 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f90));
        _opd_FUN_001a6020(uVar13 - 0x5b0 & 0xffffffff,uVar17);
        uVar21 = *(undefined4 *)(puVar10 + -0x7f6c);
        uVar17 = *(undefined4 *)(puVar10 + -0x7f2c);
        uVar26 = *(undefined4 *)(iVar15 + 0x60);
        *(float *)(iVar15 + 0xfb8) = -*(float *)(puVar7 + 0x2c);
        fVar2 = *(float *)(puVar7 + 0x2d);
        *(undefined4 *)(iVar15 + 0xfb8) = 0x44e10000;
        *(undefined4 *)(iVar15 + 0xfa8) = uVar17;
        *(undefined4 *)(iVar15 + 0xfac) = uVar17;
        *(float *)(iVar15 + 0xfbc) = -fVar2;
        *(undefined4 *)(iVar15 + 0xfbc) = 0xc47a0000;
        uVar17 = _opd_FUN_000cdc90(uVar21);
        uVar28 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7e6c));
        _opd_FUN_00b30be8(uVar26,0x2f66f6,uVar17,uVar28);
        uVar17 = *(undefined4 *)(iVar15 + 0x60);
        uVar26 = _opd_FUN_000cdc90(uVar21);
        uVar21 = *(undefined4 *)(puVar10 + -0x7e68);
      }
      else if ((int)puVar22 < 0xf8c768) {
        if (puVar22 != &UNK_00f8c747) {
LAB_00bd98b0:
          _opd_FUN_00253be0(puVar19,0xfffffffffffff200,0xfffffffffffff200,0,0);
          uVar17 = *(undefined4 *)(puVar10 + -0x7f98);
          puVar34 = (undefined4 *)(*(uint *)(puVar10 + -0x7f9c) & 0xfffffff0);
          uVar21 = *puVar34;
          uVar26 = puVar34[1];
          uVar28 = puVar34[2];
          uVar43 = puVar34[3];
          puVar34 = (undefined4 *)(uVar13 - 0x450);
          *puVar34 = uVar21;
          puVar34[1] = uVar26;
          puVar34[2] = uVar28;
          puVar34[3] = uVar43;
          puVar34 = (undefined4 *)(uVar13 - 0x460);
          *puVar34 = uVar21;
          puVar34[1] = uVar26;
          puVar34[2] = uVar28;
          puVar34[3] = uVar43;
          puVar34 = (undefined4 *)(uVar13 - 0x470);
          *puVar34 = uVar21;
          puVar34[1] = uVar26;
          puVar34[2] = uVar28;
          puVar34[3] = uVar43;
          uVar17 = _opd_FUN_000cdc90(uVar17);
          _opd_FUN_001a6020(uVar13 - 0x450 & 0xffffffff,uVar17);
          uVar17 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f94));
          _opd_FUN_001a6020(uVar13 - 0x460 & 0xffffffff,uVar17);
          uVar17 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f90));
          _opd_FUN_001a6020(uVar13 - 0x470 & 0xffffffff,uVar17);
          *(float *)(iVar15 + 0xfb8) = -*(float *)(puVar7 + 0x54);
          *(float *)(iVar15 + 0xfbc) = -*(float *)(puVar7 + 0x55);
          fVar2 = *(float *)(puVar7 + 0x58);
          fVar3 = *(float *)(puVar7 + 0x56);
          fVar8 = fVar2;
          if (fVar3 <= fVar2) {
            fVar8 = fVar3;
            fVar3 = fVar2;
          }
          fVar2 = *(float *)(puVar7 + 0x59);
          fVar4 = *(float *)(puVar7 + 0x57);
          fVar9 = fVar2;
          if (fVar4 <= fVar2) {
            fVar9 = fVar4;
            fVar4 = fVar2;
          }
          fVar2 = *(float *)(puVar10 + -0x7d34);
          uVar35 = 0x1c00;
          uVar33 = 0x1c00;
          *(float *)(iVar15 + 0xfa8) = fVar2 / (fVar3 - fVar8);
          *(float *)(iVar15 + 0xfac) = fVar2 / (fVar4 - fVar9);
          goto LAB_00bd99d0;
        }
        *(undefined2 *)(iVar15 + 0xfd4) = 2;
        _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0x9451a5);
        _opd_FUN_00b30880(*(undefined4 *)(iVar15 + 0x60),0x9451a4);
        _opd_FUN_00b30880(*(undefined4 *)(iVar15 + 0x60),0x9451a3);
        *(undefined2 *)(iVar15 + 0xfd6) = 1;
        *(undefined2 *)(iVar15 + 0xfd2) = 1;
        _opd_FUN_00253be0(puVar19,0xfffffffffffff000,0xfffffffffffff000,0,0);
        _opd_FUN_00253be0(puVar23,0xfffffffffffff000,0xfffffffffffff000,0,0);
        _opd_FUN_00253be0(puVar25,0xfffffffffffff000,0xfffffffffffff000,0,0);
        uVar17 = *(undefined4 *)(puVar10 + -0x7f98);
        puVar34 = (undefined4 *)(*(uint *)(puVar10 + -0x7f9c) & 0xfffffff0);
        uVar21 = *puVar34;
        uVar26 = puVar34[1];
        uVar28 = puVar34[2];
        uVar43 = puVar34[3];
        puVar34 = (undefined4 *)(uVar13 - 0x600);
        *puVar34 = uVar21;
        puVar34[1] = uVar26;
        puVar34[2] = uVar28;
        puVar34[3] = uVar43;
        puVar34 = (undefined4 *)(uVar13 - 0x5f0);
        *puVar34 = uVar21;
        puVar34[1] = uVar26;
        puVar34[2] = uVar28;
        puVar34[3] = uVar43;
        puVar34 = (undefined4 *)(uVar13 - 0x5e0);
        *puVar34 = uVar21;
        puVar34[1] = uVar26;
        puVar34[2] = uVar28;
        puVar34[3] = uVar43;
        uVar17 = _opd_FUN_000cdc90(uVar17);
        _opd_FUN_001a6020(uVar13 - 0x600 & 0xffffffff,uVar17);
        uVar17 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f94));
        _opd_FUN_001a6020(uVar13 - 0x5f0 & 0xffffffff,uVar17);
        uVar17 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f90));
        _opd_FUN_001a6020(uVar13 - 0x5e0 & 0xffffffff,uVar17);
        uVar21 = *(undefined4 *)(puVar10 + -0x7f6c);
        uVar17 = *(undefined4 *)(puVar10 + -0x7ec8);
        uVar26 = *(undefined4 *)(iVar15 + 0x60);
        *(float *)(iVar15 + 0xfb8) = -*(float *)(puVar7 + 0x26);
        fVar2 = *(float *)(puVar7 + 0x27);
        *(undefined4 *)(iVar15 + 0xfb8) = 0x43480000;
        *(undefined4 *)(iVar15 + 0xfa8) = uVar17;
        *(undefined4 *)(iVar15 + 0xfac) = uVar17;
        *(float *)(iVar15 + 0xfbc) = -fVar2;
        *(undefined4 *)(iVar15 + 0xfbc) = 0xc4c80000;
        uVar17 = _opd_FUN_000cdc90(uVar21);
        uVar28 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7ec0));
        _opd_FUN_00b30be8(uVar26,0x2f66f6,uVar17,uVar28);
        uVar17 = *(undefined4 *)(iVar15 + 0x60);
        uVar26 = _opd_FUN_000cdc90(uVar21);
        uVar28 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7ebc));
        _opd_FUN_00b30be8(uVar17,0x2f66f7,uVar26,uVar28);
        uVar17 = *(undefined4 *)(iVar15 + 0x60);
        uVar26 = _opd_FUN_000cdc90(uVar21);
        uVar21 = *(undefined4 *)(puVar10 + -0x7eb8);
      }
      else if (puVar22 == &UNK_00f8c787) {
        *(undefined2 *)(iVar15 + 0xfd4) = 3;
        _opd_FUN_00b30880(*(undefined4 *)(iVar15 + 0x60),0x9451a5);
        _opd_FUN_00b30880(*(undefined4 *)(iVar15 + 0x60),0x9451a4);
        _opd_FUN_00b30880(*(undefined4 *)(iVar15 + 0x60),0x9451a3);
        *(undefined2 *)(iVar15 + 0xfd6) = 1;
        *(undefined2 *)(iVar15 + 0xfd2) = 1;
        _opd_FUN_00253be0(puVar19,0xfffffffffffff000,0xfffffffffffff000,0,0);
        _opd_FUN_00253be0(puVar23,0xfffffffffffff000,0xfffffffffffff000,0,0);
        _opd_FUN_00253be0(puVar24,0xfffffffffffff000,0xfffffffffffff000,0,0);
        _opd_FUN_00253be0(puVar25,0xfffffffffffff000,0xfffffffffffff000,0,0);
        uVar17 = *(undefined4 *)(puVar10 + -0x7f98);
        puVar34 = (undefined4 *)(*(uint *)(puVar10 + -0x7f9c) & 0xfffffff0);
        uVar21 = *puVar34;
        uVar26 = puVar34[1];
        uVar28 = puVar34[2];
        uVar43 = puVar34[3];
        puVar34 = (undefined4 *)(uVar13 - 0x4b0);
        *puVar34 = uVar21;
        puVar34[1] = uVar26;
        puVar34[2] = uVar28;
        puVar34[3] = uVar43;
        puVar34 = (undefined4 *)(uVar13 - 0x4a0);
        *puVar34 = uVar21;
        puVar34[1] = uVar26;
        puVar34[2] = uVar28;
        puVar34[3] = uVar43;
        puVar34 = (undefined4 *)(uVar13 - 0x500);
        *puVar34 = uVar21;
        puVar34[1] = uVar26;
        puVar34[2] = uVar28;
        puVar34[3] = uVar43;
        uVar17 = _opd_FUN_000cdc90(uVar17);
        _opd_FUN_001a6020(uVar13 - 0x4b0 & 0xffffffff,uVar17);
        uVar17 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f94));
        _opd_FUN_001a6020(uVar13 - 0x4a0 & 0xffffffff,uVar17);
        uVar17 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f90));
        _opd_FUN_001a6020(uVar13 - 0x500 & 0xffffffff,uVar17);
        uVar26 = *(undefined4 *)(puVar10 + -0x7f6c);
        uVar17 = *(undefined4 *)(puVar10 + -0x7e1c);
        uVar21 = *(undefined4 *)(puVar10 + -0x7f24);
        uVar28 = *(undefined4 *)(iVar15 + 0x60);
        *(float *)(iVar15 + 0xfb8) = -*(float *)(puVar7 + 0x42);
        fVar2 = *(float *)(puVar7 + 0x43);
        *(undefined4 *)(iVar15 + 0xfb8) = 0x42200000;
        *(undefined4 *)(iVar15 + 0xfa8) = uVar17;
        *(undefined4 *)(iVar15 + 0xfac) = uVar21;
        *(float *)(iVar15 + 0xfbc) = -fVar2;
        *(undefined4 *)(iVar15 + 0xfbc) = *(undefined4 *)(puVar10 + -0x7e14);
        uVar17 = _opd_FUN_000cdc90(uVar26);
        uVar21 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7e0c));
        _opd_FUN_00b30be8(uVar28,0x2f66f6,uVar17,uVar21);
        uVar17 = *(undefined4 *)(iVar15 + 0x60);
        uVar21 = _opd_FUN_000cdc90(uVar26);
        uVar28 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7e08));
        _opd_FUN_00b30be8(uVar17,0x2f66f7,uVar21,uVar28);
        uVar17 = *(undefined4 *)(iVar15 + 0x60);
        uVar21 = _opd_FUN_000cdc90(uVar26);
        uVar28 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7e04));
        _opd_FUN_00b30be8(uVar17,0x2f66f8,uVar21,uVar28);
        uVar17 = *(undefined4 *)(iVar15 + 0x60);
        uVar26 = _opd_FUN_000cdc90(uVar26);
        uVar21 = *(undefined4 *)(puVar10 + -0x7e00);
      }
      else {
        if (puVar22 != &UNK_00f8ca67) goto LAB_00bd98b0;
        *(undefined2 *)(iVar15 + 0xfd4) = 3;
        _opd_FUN_00b30880(*(undefined4 *)(iVar15 + 0x60),0x9451a5);
        _opd_FUN_00b30880(*(undefined4 *)(iVar15 + 0x60),0x9451a4);
        _opd_FUN_00b30880(*(undefined4 *)(iVar15 + 0x60),0x9451a3);
        *(undefined2 *)(iVar15 + 0xfd6) = 1;
        *(undefined2 *)(iVar15 + 0xfd2) = 1;
        _opd_FUN_00253be0(puVar19,0xfffffffffffff000,0xfffffffffffff000,0,0);
        _opd_FUN_00253be0(puVar23,0xfffffffffffff000,0xfffffffffffff000,0,0);
        _opd_FUN_00253be0(puVar24,0xfffffffffffff000,0xfffffffffffff000,0,0);
        _opd_FUN_00253be0(puVar25,0xfffffffffffff000,0xfffffffffffff000,0,0);
        uVar17 = *(undefined4 *)(puVar10 + -0x7f98);
        puVar34 = (undefined4 *)(*(uint *)(puVar10 + -0x7f9c) & 0xfffffff0);
        uVar21 = *puVar34;
        uVar26 = puVar34[1];
        uVar28 = puVar34[2];
        uVar43 = puVar34[3];
        puVar34 = (undefined4 *)(uVar13 - 0x4e0);
        *puVar34 = uVar21;
        puVar34[1] = uVar26;
        puVar34[2] = uVar28;
        puVar34[3] = uVar43;
        puVar34 = (undefined4 *)(uVar13 - 0x4d0);
        *puVar34 = uVar21;
        puVar34[1] = uVar26;
        puVar34[2] = uVar28;
        puVar34[3] = uVar43;
        puVar34 = (undefined4 *)(uVar13 - 0x4c0);
        *puVar34 = uVar21;
        puVar34[1] = uVar26;
        puVar34[2] = uVar28;
        puVar34[3] = uVar43;
        uVar17 = _opd_FUN_000cdc90(uVar17);
        _opd_FUN_001a6020(uVar13 - 0x4e0 & 0xffffffff,uVar17);
        uVar17 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f94));
        _opd_FUN_001a6020(uVar13 - 0x4d0 & 0xffffffff,uVar17);
        uVar17 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f90));
        _opd_FUN_001a6020(uVar13 - 0x4c0 & 0xffffffff,uVar17);
        uVar26 = *(undefined4 *)(puVar10 + -0x7f6c);
        uVar17 = *(undefined4 *)(puVar10 + -0x7e4c);
        uVar21 = *(undefined4 *)(puVar10 + -0x7e44);
        uVar28 = *(undefined4 *)(iVar15 + 0x60);
        *(float *)(iVar15 + 0xfb8) = -*(float *)(puVar7 + 0x4a);
        fVar2 = *(float *)(puVar7 + 0x4b);
        *(undefined4 *)(iVar15 + 0xfb8) = *(undefined4 *)(puVar10 + -0x7e3c);
        *(undefined4 *)(iVar15 + 0xfa8) = uVar17;
        *(undefined4 *)(iVar15 + 0xfac) = uVar21;
        *(float *)(iVar15 + 0xfbc) = -fVar2;
        *(undefined4 *)(iVar15 + 0xfbc) = *(undefined4 *)(puVar10 + -0x7e34);
        uVar17 = _opd_FUN_000cdc90(uVar26);
        uVar21 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7e2c));
        _opd_FUN_00b30be8(uVar28,0x2f66f6,uVar17,uVar21);
        uVar17 = *(undefined4 *)(iVar15 + 0x60);
        uVar21 = _opd_FUN_000cdc90(uVar26);
        uVar28 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7e28));
        _opd_FUN_00b30be8(uVar17,0x2f66f7,uVar21,uVar28);
        uVar17 = *(undefined4 *)(iVar15 + 0x60);
        uVar21 = _opd_FUN_000cdc90(uVar26);
        uVar28 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7e24));
        _opd_FUN_00b30be8(uVar17,0x2f66f8,uVar21,uVar28);
        uVar17 = *(undefined4 *)(iVar15 + 0x60);
        uVar26 = _opd_FUN_000cdc90(uVar26);
        uVar21 = *(undefined4 *)(puVar10 + -0x7e20);
      }
    }
    else if (puVar22 == &UNK_00f8cae7) {
      *(undefined2 *)(iVar15 + 0xfd4) = 1;
      _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0x9451a5);
      _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0x9451a4);
      _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0x9451a3);
      _opd_FUN_00253be0(puVar19,0xfffffffffffff000,0xfffffffffffff000,0,0);
      _opd_FUN_00253be0(puVar25,0xfffffffffffff000,0xfffffffffffff000,0,0);
      uVar17 = *(undefined4 *)(puVar10 + -0x7f98);
      puVar34 = (undefined4 *)(*(uint *)(puVar10 + -0x7f9c) & 0xfffffff0);
      uVar21 = *puVar34;
      uVar26 = puVar34[1];
      uVar28 = puVar34[2];
      uVar43 = puVar34[3];
      puVar34 = (undefined4 *)(uVar13 - 0x5a0);
      *puVar34 = uVar21;
      puVar34[1] = uVar26;
      puVar34[2] = uVar28;
      puVar34[3] = uVar43;
      puVar34 = (undefined4 *)(uVar13 - 0x590);
      *puVar34 = uVar21;
      puVar34[1] = uVar26;
      puVar34[2] = uVar28;
      puVar34[3] = uVar43;
      puVar34 = (undefined4 *)(uVar13 - 0x4f0);
      *puVar34 = uVar21;
      puVar34[1] = uVar26;
      puVar34[2] = uVar28;
      puVar34[3] = uVar43;
      uVar17 = _opd_FUN_000cdc90(uVar17);
      _opd_FUN_001a6020(uVar13 - 0x5a0 & 0xffffffff,uVar17);
      uVar17 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f94));
      _opd_FUN_001a6020(uVar13 - 0x590 & 0xffffffff,uVar17);
      uVar17 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f90));
      _opd_FUN_001a6020(uVar13 - 0x4f0 & 0xffffffff,uVar17);
      uVar26 = *(undefined4 *)(puVar10 + -0x7f6c);
      uVar17 = *(undefined4 *)(puVar10 + -0x7e64);
      uVar21 = *(undefined4 *)(puVar10 + -0x7e5c);
      uVar28 = *(undefined4 *)(iVar15 + 0x60);
      *(float *)(iVar15 + 0xfb8) = -*(float *)(puVar7 + 0x44);
      fVar2 = *(float *)(puVar7 + 0x45);
      *(undefined4 *)(iVar15 + 0xfb8) = 0x44fa0000;
      *(undefined4 *)(iVar15 + 0xfa8) = uVar17;
      *(undefined4 *)(iVar15 + 0xfac) = uVar17;
      *(float *)(iVar15 + 0xfbc) = -fVar2;
      *(undefined4 *)(iVar15 + 0xfbc) = uVar21;
      uVar17 = _opd_FUN_000cdc90(uVar26);
      uVar21 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7e54));
      _opd_FUN_00b30be8(uVar28,0x2f66f6,uVar17,uVar21);
      uVar17 = *(undefined4 *)(iVar15 + 0x60);
      uVar26 = _opd_FUN_000cdc90(uVar26);
      uVar21 = *(undefined4 *)(puVar10 + -0x7e50);
    }
    else if ((int)puVar22 < 0xf8cae8) {
      if (puVar22 != &UNK_00f8cac7) goto LAB_00bd98b0;
      *(undefined2 *)(iVar15 + 0xfd4) = 3;
      _opd_FUN_00b30880(*(undefined4 *)(iVar15 + 0x60),0x9451a5);
      _opd_FUN_00b30880(*(undefined4 *)(iVar15 + 0x60),0x9451a4);
      _opd_FUN_00b30880(*(undefined4 *)(iVar15 + 0x60),0x9451a3);
      _opd_FUN_00253be0(puVar19,0xfffffffffffff000,0xfffffffffffff000,0,0);
      _opd_FUN_00253be0(puVar23,0xfffffffffffff000,0xfffffffffffff000,0,0);
      _opd_FUN_00253be0(puVar24,0xfffffffffffff000,0xfffffffffffff000,0,0);
      _opd_FUN_00253be0(puVar25,0xfffffffffffff000,0xfffffffffffff000,0,0);
      uVar17 = *(undefined4 *)(puVar10 + -0x7f98);
      puVar34 = (undefined4 *)(*(uint *)(puVar10 + -0x7f9c) & 0xfffffff0);
      uVar21 = *puVar34;
      uVar26 = puVar34[1];
      uVar28 = puVar34[2];
      uVar43 = puVar34[3];
      puVar34 = (undefined4 *)(uVar13 - 0x670);
      *puVar34 = uVar21;
      puVar34[1] = uVar26;
      puVar34[2] = uVar28;
      puVar34[3] = uVar43;
      puVar34 = (undefined4 *)(uVar13 - 0x660);
      *puVar34 = uVar21;
      puVar34[1] = uVar26;
      puVar34[2] = uVar28;
      puVar34[3] = uVar43;
      puVar34 = (undefined4 *)(uVar13 - 0x6a0);
      *puVar34 = uVar21;
      puVar34[1] = uVar26;
      puVar34[2] = uVar28;
      puVar34[3] = uVar43;
      uVar17 = _opd_FUN_000cdc90(uVar17);
      _opd_FUN_001a6020(uVar13 - 0x670 & 0xffffffff,uVar17);
      uVar17 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f94));
      _opd_FUN_001a6020(uVar13 - 0x660 & 0xffffffff,uVar17);
      uVar17 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f90));
      _opd_FUN_001a6020(uVar13 - 0x6a0 & 0xffffffff,uVar17);
      uVar26 = *(undefined4 *)(puVar10 + -0x7f6c);
      uVar17 = *(undefined4 *)(puVar10 + -0x7e94);
      uVar21 = *(undefined4 *)(puVar10 + -0x7e84);
      uVar28 = *(undefined4 *)(iVar15 + 0x60);
      *(float *)(iVar15 + 0xfb8) = -*(float *)(puVar7 + 0xe);
      fVar2 = *(float *)(puVar7 + 0xf);
      *(undefined4 *)(iVar15 + 0xfa8) = uVar17;
      *(undefined4 *)(iVar15 + 0xfac) = uVar17;
      *(float *)(iVar15 + 0xfbc) = -fVar2;
      *(undefined4 *)(iVar15 + 0xfb8) = *(undefined4 *)(puVar10 + -0x7e8c);
      *(undefined4 *)(iVar15 + 0xfbc) = uVar21;
      uVar17 = _opd_FUN_000cdc90(uVar26);
      uVar21 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7e7c));
      _opd_FUN_00b30be8(uVar28,0x2f66f6,uVar17,uVar21);
      _opd_FUN_00b18ab0();
      uVar17 = *(undefined4 *)(iVar15 + 0x60);
      uVar21 = _opd_FUN_000cdc90(uVar26);
      uVar28 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7e78));
      _opd_FUN_00b30be8(uVar17,0x2f66f7,uVar21,uVar28);
      uVar17 = *(undefined4 *)(iVar15 + 0x60);
      uVar21 = _opd_FUN_000cdc90(uVar26);
      uVar28 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7e74));
      _opd_FUN_00b30be8(uVar17,0x2f66f8,uVar21,uVar28);
      uVar17 = *(undefined4 *)(iVar15 + 0x60);
      uVar26 = _opd_FUN_000cdc90(uVar26);
      uVar21 = *(undefined4 *)(puVar10 + -0x7e70);
    }
    else if (puVar22 == (undefined *)0xf8cb07) {
      *(undefined2 *)(iVar15 + 0xfd4) = 3;
      _opd_FUN_00b30880(*(undefined4 *)(iVar15 + 0x60),0x9451a5);
      _opd_FUN_00b30880(*(undefined4 *)(iVar15 + 0x60),0x9451a4);
      _opd_FUN_00b30880(*(undefined4 *)(iVar15 + 0x60),0x9451a3);
      *(undefined2 *)(iVar15 + 0xfd6) = 1;
      *(undefined2 *)(iVar15 + 0xfd2) = 1;
      _opd_FUN_00253be0(puVar19,0xfffffffffffff000,0xfffffffffffff000,0,0);
      _opd_FUN_00253be0(puVar23,0xfffffffffffff000,0xfffffffffffff000,0,0);
      _opd_FUN_00253be0(puVar24,0xfffffffffffff000,0xfffffffffffff000,0,0);
      _opd_FUN_00253be0(puVar25,0xfffffffffffff000,0xfffffffffffff000,0,0);
      uVar17 = *(undefined4 *)(puVar10 + -0x7f98);
      puVar34 = (undefined4 *)(*(uint *)(puVar10 + -0x7f9c) & 0xfffffff0);
      uVar21 = *puVar34;
      uVar26 = puVar34[1];
      uVar28 = puVar34[2];
      uVar43 = puVar34[3];
      puVar34 = (undefined4 *)(uVar13 - 0x570);
      *puVar34 = uVar21;
      puVar34[1] = uVar26;
      puVar34[2] = uVar28;
      puVar34[3] = uVar43;
      puVar34 = (undefined4 *)(uVar13 - 0x560);
      *puVar34 = uVar21;
      puVar34[1] = uVar26;
      puVar34[2] = uVar28;
      puVar34[3] = uVar43;
      puVar34 = (undefined4 *)(uVar13 - 0x550);
      *puVar34 = uVar21;
      puVar34[1] = uVar26;
      puVar34[2] = uVar28;
      puVar34[3] = uVar43;
      uVar17 = _opd_FUN_000cdc90(uVar17);
      _opd_FUN_001a6020(uVar13 - 0x570 & 0xffffffff,uVar17);
      uVar17 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f94));
      _opd_FUN_001a6020(uVar13 - 0x560 & 0xffffffff,uVar17);
      uVar17 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f90));
      _opd_FUN_001a6020(uVar13 - 0x550 & 0xffffffff,uVar17);
      uVar26 = *(undefined4 *)(puVar10 + -0x7f6c);
      uVar17 = *(undefined4 *)(puVar10 + -0x7dd4);
      uVar21 = *(undefined4 *)(puVar10 + -0x7eb4);
      uVar28 = *(undefined4 *)(iVar15 + 0x60);
      *(float *)(iVar15 + 0xfb8) = -*(float *)(puVar7 + 0x38);
      fVar2 = *(float *)(puVar7 + 0x39);
      *(undefined4 *)(iVar15 + 0xfb8) = *(undefined4 *)(puVar10 + -0x7dcc);
      *(undefined4 *)(iVar15 + 0xfa8) = uVar17;
      *(undefined4 *)(iVar15 + 0xfac) = uVar21;
      *(float *)(iVar15 + 0xfbc) = -fVar2;
      *(undefined4 *)(iVar15 + 0xfbc) = *(undefined4 *)(puVar10 + -0x7e84);
      uVar17 = _opd_FUN_000cdc90(uVar26);
      uVar21 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7dc4));
      _opd_FUN_00b30be8(uVar28,0x2f66f6,uVar17,uVar21);
      uVar17 = *(undefined4 *)(iVar15 + 0x60);
      uVar21 = _opd_FUN_000cdc90(uVar26);
      uVar28 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7dc0));
      _opd_FUN_00b30be8(uVar17,0x2f66f7,uVar21,uVar28);
      uVar17 = *(undefined4 *)(iVar15 + 0x60);
      uVar21 = _opd_FUN_000cdc90(uVar26);
      uVar28 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7dbc));
      _opd_FUN_00b30be8(uVar17,0x2f66f8,uVar21,uVar28);
      uVar17 = *(undefined4 *)(iVar15 + 0x60);
      uVar26 = _opd_FUN_000cdc90(uVar26);
      uVar21 = *(undefined4 *)(puVar10 + -0x7db8);
    }
    else {
      if (puVar22 != (undefined *)0xf8cb67) goto LAB_00bd98b0;
      *(undefined2 *)(iVar15 + 0xfd4) = 2;
      _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0x9451a5);
      _opd_FUN_00b30880(*(undefined4 *)(iVar15 + 0x60),0x9451a4);
      _opd_FUN_00b30880(*(undefined4 *)(iVar15 + 0x60),0x9451a3);
      *(undefined2 *)(iVar15 + 0xfd2) = 0;
      _opd_FUN_00253be0(puVar19,0xfffffffffffff000,0xfffffffffffff000,0,0);
      _opd_FUN_00253be0(puVar23,0xfffffffffffff000,0xfffffffffffff000,0,0);
      _opd_FUN_00253be0(puVar24,0xfffffffffffff000,0xfffffffffffff000,0,0);
      _opd_FUN_00253be0(puVar25,0xfffffffffffff000,0xfffffffffffff000,0,0);
      uVar17 = *(undefined4 *)(puVar10 + -0x7f98);
      puVar34 = (undefined4 *)(*(uint *)(puVar10 + -0x7f9c) & 0xfffffff0);
      uVar21 = *puVar34;
      uVar26 = puVar34[1];
      uVar28 = puVar34[2];
      uVar43 = puVar34[3];
      puVar34 = (undefined4 *)(uVar13 - 0x540);
      *puVar34 = uVar21;
      puVar34[1] = uVar26;
      puVar34[2] = uVar28;
      puVar34[3] = uVar43;
      puVar34 = (undefined4 *)(uVar13 - 0x530);
      *puVar34 = uVar21;
      puVar34[1] = uVar26;
      puVar34[2] = uVar28;
      puVar34[3] = uVar43;
      puVar34 = (undefined4 *)(uVar13 - 0x3e0);
      *puVar34 = uVar21;
      puVar34[1] = uVar26;
      puVar34[2] = uVar28;
      puVar34[3] = uVar43;
      uVar17 = _opd_FUN_000cdc90(uVar17);
      _opd_FUN_001a6020(uVar13 - 0x540 & 0xffffffff,uVar17);
      uVar17 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f94));
      _opd_FUN_001a6020(uVar13 - 0x530 & 0xffffffff,uVar17);
      uVar17 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7f90));
      _opd_FUN_001a6020(uVar13 - 0x3e0 & 0xffffffff,uVar17);
      uVar21 = *(undefined4 *)(puVar10 + -0x7f6c);
      uVar17 = *(undefined4 *)(puVar10 + -0x7db4);
      uVar26 = *(undefined4 *)(iVar15 + 0x60);
      *(float *)(iVar15 + 0xfb8) = -*(float *)(puVar7 + 0x66);
      fVar2 = *(float *)(puVar7 + 0x67);
      *(undefined4 *)(iVar15 + 0xfb8) = *(undefined4 *)(puVar10 + -0x7dac);
      *(undefined4 *)(iVar15 + 0xfa8) = uVar17;
      *(undefined4 *)(iVar15 + 0xfac) = uVar17;
      *(float *)(iVar15 + 0xfbc) = -fVar2;
      *(undefined4 *)(iVar15 + 0xfbc) = *(undefined4 *)(puVar10 + -0x7da4);
      uVar17 = _opd_FUN_000cdc90(uVar21);
      uVar28 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7d9c));
      _opd_FUN_00b30be8(uVar26,0x2f66f6,uVar17,uVar28);
      uVar17 = *(undefined4 *)(iVar15 + 0x60);
      uVar26 = _opd_FUN_000cdc90(uVar21);
      uVar28 = _opd_FUN_000cdc90(*(undefined4 *)(puVar10 + -0x7d98));
      _opd_FUN_00b30be8(uVar17,0x2f66f7,uVar26,uVar28);
      uVar17 = *(undefined4 *)(iVar15 + 0x60);
      uVar26 = _opd_FUN_000cdc90(uVar21);
      uVar21 = *(undefined4 *)(puVar10 + -0x7d94);
    }
  }
  uVar21 = _opd_FUN_000cdc90(uVar21);
  uVar35 = 0x2000;
  uVar33 = 0x2000;
  _opd_FUN_00b30be8(uVar17,0x2f66f9,uVar26,uVar21);
LAB_00bd99d0:
  _opd_FUN_00253b68(puVar19,uVar35,uVar33,0);
  _opd_FUN_00256188((double)*(float *)(puVar10 + -0x7fdc),(double)*(float *)(puVar10 + -0x7fdc),
                    (double)*(float *)(puVar10 + -0x7d2c),(double)*(float *)(puVar10 + -0x7d2c),
                    *(undefined4 *)(iVar15 + 0x60),puVar19);
  _opd_FUN_00253b68(puVar23,uVar35,uVar33,0);
  _opd_FUN_00256188((double)*(float *)(puVar10 + -0x7fdc),(double)*(float *)(puVar10 + -0x7fdc),
                    (double)*(float *)(puVar10 + -0x7d2c),(double)*(float *)(puVar10 + -0x7d2c),
                    *(undefined4 *)(iVar15 + 0x60),puVar23);
  _opd_FUN_00253b68(puVar24,uVar35,uVar33,0);
  _opd_FUN_00256188((double)*(float *)(puVar10 + -0x7fdc),(double)*(float *)(puVar10 + -0x7fdc),
                    (double)*(float *)(puVar10 + -0x7d2c),(double)*(float *)(puVar10 + -0x7d2c),
                    *(undefined4 *)(iVar15 + 0x60),puVar24);
  _opd_FUN_00b30a98(*(undefined4 *)(iVar15 + 0x60),0x2f66f6,0x7f);
  _opd_FUN_00b30a98(*(undefined4 *)(iVar15 + 0x60),0x2f66f7,0x20);
  _opd_FUN_00b30a98(*(undefined4 *)(iVar15 + 0x60),0x2f66f8,0x20);
  _opd_FUN_00253b68(puVar25,uVar35,uVar33,0);
  _opd_FUN_00256188((double)*(float *)(puVar10 + -0x7fdc),(double)*(float *)(puVar10 + -0x7fdc),
                    (double)*(float *)(puVar10 + -0x7d2c),(double)*(float *)(puVar10 + -0x7d2c),
                    *(undefined4 *)(iVar15 + 0x60),puVar25);
  _opd_FUN_00b30a98(*(undefined4 *)(iVar15 + 0x60),0x2f66f9,0x7f);
  *(undefined4 *)(iVar15 + 0xfb0) = *(undefined4 *)(iVar15 + 0xfa8);
  *(undefined4 *)(iVar15 + 0xfb4) = *(undefined4 *)(iVar15 + 0xfac);
  *(undefined4 *)(iVar15 + 0xfc0) = *(undefined4 *)(iVar15 + 0xfb8);
  *(undefined4 *)(iVar15 + 0xfc4) = *(undefined4 *)(iVar15 + 0xfbc);
  *(undefined8 *)(iVar15 + 0xfd8) = *(undefined8 *)(iVar15 + 0xfc8);
  *(undefined8 *)(iVar15 + 0xfe0) = *(undefined8 *)(iVar15 + 0xfd0);
  _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0x57d678);
  iVar16 = _opd_FUN_00b188b0();
  if (iVar16 != 0) {
    iVar16 = _opd_FUN_00242bf0(*(undefined4 *)(iVar15 + 0x60),0x57d678);
    uVar17 = *(undefined4 *)(iVar15 + 0x60);
    uVar21 = _opd_FUN_00242bf0(uVar17,0x2a8fb2);
    uVar17 = _opd_FUN_00255678(uVar17,uVar21);
    _opd_FUN_00255960(iVar16,uVar17);
    iVar20 = *(ushort *)(iVar16 + 0x1a) - 1;
    if (-1 < iVar20) {
      uVar5 = *(uint *)(puVar10 + -0x7d88);
      puVar7[0xbb] = uVar13 - 0x280 & 0xffffffff;
      puVar7[0xbc] = uVar13 - 0x290 & 0xffffffff;
      puVar7[0xba] = uVar13 - 0x270 & 0xffffffff;
      uVar29 = (ulonglong)*(uint *)(puVar10 + -0x7d90);
      uVar11 = *(uint *)(puVar10 + -0x7d8c);
      puVar7[0xbd] = uVar31;
      iVar32 = iVar16 + 0x1c;
      puVar7[0xb9] = uVar29;
      puVar7[0xbe] = (ulonglong)uVar11;
      do {
        puVar34 = (undefined4 *)((uint)uVar29 & 0xfffffff0);
        uVar17 = *puVar34;
        uVar21 = puVar34[1];
        uVar26 = puVar34[2];
        uVar28 = puVar34[3];
        iVar18 = *(ushort *)(iVar32 + 4) - 1;
        uVar11 = *(uint *)(puVar10 + -0x7f9c);
        *(undefined4 *)((int)puVar7 + 0x3d4) = 0xbf800000;
        *(undefined4 *)(puVar7 + 0x7a) = 0;
        *(undefined4 *)(puVar7 + 0x7b) = 0;
        puVar34 = (undefined4 *)((uint)puVar7[0xbe] & 0xfffffff0);
        uVar43 = *puVar34;
        uVar44 = puVar34[1];
        uVar45 = puVar34[2];
        uVar46 = puVar34[3];
        puVar34 = (undefined4 *)(uVar11 & 0xfffffff0);
        uVar52 = *puVar34;
        uVar53 = puVar34[1];
        uVar54 = puVar34[2];
        uVar55 = puVar34[3];
        *(undefined4 *)((int)puVar7 + 0x3dc) = 0x3f800000;
        puVar34 = (undefined4 *)(uVar5 & 0xfffffff0);
        uVar56 = *puVar34;
        uVar61 = puVar34[1];
        uVar62 = puVar34[2];
        uVar63 = puVar34[3];
        puVar34 = (undefined4 *)((uint)puVar7[0xbd] & 0xfffffff0);
        *puVar34 = uVar17;
        puVar34[1] = uVar21;
        puVar34[2] = uVar26;
        puVar34[3] = uVar28;
        puVar34 = (undefined4 *)((uint)puVar7[0xbc] & 0xfffffff0);
        *puVar34 = uVar43;
        puVar34[1] = uVar44;
        puVar34[2] = uVar45;
        puVar34[3] = uVar46;
        puVar34 = (undefined4 *)((uint)puVar7[0xbb] & 0xfffffff0);
        *puVar34 = uVar56;
        puVar34[1] = uVar61;
        puVar34[2] = uVar62;
        puVar34[3] = uVar63;
        puVar34 = (undefined4 *)((uint)puVar7[0xba] & 0xfffffff0);
        *puVar34 = uVar52;
        puVar34[1] = uVar53;
        puVar34[2] = uVar54;
        puVar34[3] = uVar55;
        if (-1 < iVar18) {
          *(undefined4 *)(puVar7 + 0xc1) = 0;
          dVar39 = (double)*(float *)(puVar7 + 0xc1);
          auVar66 = *(undefined1 (*) [16])(puVar7 + 0x7a);
          if (iVar18 == 0) goto LAB_00bda554;
          do {
            dVar40 = (double)*(float *)(puVar10 + -0x7d24);
            fVar2 = *(float *)(puVar10 + -0x7fdc);
            dVar41 = dVar40;
            if (iVar18 != 1) {
              *(float *)(puVar7 + 0xb8) = (float)dVar39;
              auVar42 = ZEXT416(0xffffffff);
              auVar50 = auVar42 | auVar42 << 0x20 | auVar42 << 0x40 | auVar42 << 0x60;
              auVar42 = ZEXT416(1);
              auVar49 = auVar42 | auVar42 << 0x20 | auVar42 << 0x40 | auVar42 << 0x60;
              auVar51 = vectorShiftLeftIntegerWord(auVar50,auVar50);
              auVar50 = loadVectorElementWordIndexed(uVar13 - 0x710,0x5c0);
              auVar42 = ZEXT416(2);
              auVar47 = auVar42 | auVar42 << 0x20 | auVar42 << 0x40 | auVar42 << 0x60;
              auVar50 = auVar50 >> 0x60;
              auVar48 = auVar50 & (undefined1  [16])0xffffffff | auVar50 << 0x20 | auVar50 << 0x40 |
                        auVar50 << 0x60;
              auVar50 = vectorMultiplyAddFloatingPoint
                                  (auVar48,*(undefined1 (*) [16])
                                            (*(uint *)(puVar10 + -0x7d84) & 0xfffffff0),
                                   (undefined1  [16])0x0);
              puVar34 = (undefined4 *)(uVar5 & 0xfffffff0);
              uStack_2c0 = *puVar34;
              uStack_2bc = puVar34[1];
              uStack_2b8 = puVar34[2];
              uStack_2b4 = puVar34[3];
              auVar42 = vectorConditionalSelect
                                  (*(undefined1 (*) [16])(*(uint *)(puVar10 + -0x7d80) & 0xfffffff0)
                                   ,auVar50,auVar51);
              auVar50 = vectorAddFloatingPoint(auVar50,auVar42);
              auVar42 = ZEXT416(3);
              auVar57 = vectorConvertToSignedFixedPointWordSaturate(auVar50,0);
              auVar50 = vectorConvertFromSignedFixedPointWord(auVar57,0);
              auVar58 = vectorLogicalAnd(auVar57,auVar42 | auVar42 << 0x20 | auVar42 << 0x40 |
                                                 auVar42 << 0x60);
              auVar42._0_4_ = auVar58._0_4_ + auVar49._0_4_;
              auVar42._8_8_ = in_vs37._8_8_;
              auVar42._4_4_ = auVar58._4_4_ + auVar49._4_4_;
              auVar57._0_8_ = auVar42._0_8_;
              auVar57._8_4_ = auVar58._8_4_ + auVar49._8_4_;
              auVar57._12_4_ = auVar58._12_4_ + auVar49._12_4_;
              auVar42 = vectorLogicalAnd(auVar58,auVar49);
              auVar59 = vectorLogicalAnd(auVar58,auVar47);
              auVar58 = vectorLogicalAnd(auVar57,auVar49);
              auVar49 = vectorCompareEqualToUnsignedWord(auVar42,(undefined1  [16])0x0);
              auVar42 = vectorNegativeMultiplySubtractFloatingPoint
                                  (auVar50,*(undefined1 (*) [16])
                                            (*(uint *)(puVar10 + -0x7d7c) & 0xfffffff0),auVar48);
              auVar47 = vectorLogicalAnd(auVar57,auVar47);
              auVar48 = vectorCompareEqualToUnsignedWord(auVar58,(undefined1  [16])0x0);
              auVar60 = vectorCompareEqualToUnsignedWord(auVar59,(undefined1  [16])0x0);
              auVar47 = vectorCompareEqualToUnsignedWord(auVar47,(undefined1  [16])0x0);
              auVar50 = vectorNegativeMultiplySubtractFloatingPoint
                                  (auVar50,*(undefined1 (*) [16])
                                            (*(uint *)(puVar10 + -0x7d78) & 0xfffffff0),auVar42);
              auVar42 = vectorMultiplyAddFloatingPoint(auVar50,auVar50,(undefined1  [16])0x0);
              auVar59 = vectorMultiplyAddFloatingPoint
                                  (auVar42,*(undefined1 (*) [16])
                                            (*(uint *)(puVar10 + -0x7d74) & 0xfffffff0),
                                   *(undefined1 (*) [16])(*(uint *)(puVar10 + -0x7d70) & 0xfffffff0)
                                  );
              auVar58 = vectorMultiplyAddFloatingPoint
                                  (auVar42,*(undefined1 (*) [16])
                                            (*(uint *)(puVar10 + -0x7d6c) & 0xfffffff0),
                                   *(undefined1 (*) [16])(*(uint *)(puVar10 + -0x7d68) & 0xfffffff0)
                                  );
              auVar57 = vectorMultiplyAddFloatingPoint(auVar42,auVar50,(undefined1  [16])0x0);
              auVar59 = vectorMultiplyAddFloatingPoint
                                  (auVar59,auVar42,
                                   *(undefined1 (*) [16])(*(uint *)(puVar10 + -0x7d64) & 0xfffffff0)
                                  );
              auVar58 = vectorMultiplyAddFloatingPoint
                                  (auVar58,auVar42,
                                   *(undefined1 (*) [16])(*(uint *)(puVar10 + -0x7d60) & 0xfffffff0)
                                  );
              auVar59 = vectorMultiplyAddFloatingPoint
                                  (auVar59,auVar42,
                                   *(undefined1 (*) [16])(*(uint *)(puVar10 + -0x7d5c) & 0xfffffff0)
                                  );
              auVar50 = vectorMultiplyAddFloatingPoint(auVar58,auVar57,auVar50);
              auVar42 = vectorConditionalSelect(auVar59,auVar50,auVar49);
              auVar57 = vectorConditionalSelect(auVar59,auVar50,auVar48);
              auVar50 = vectorConditionalSelect(auVar51 ^ auVar42,auVar42,auVar60);
              auVar57 = vectorConditionalSelect(auVar51 ^ auVar57,auVar57,auVar47);
              auVar42 = vectorConditionalSelect
                                  ((undefined1  [16])0x0,auVar57,
                                   *(undefined1 (*) [16])(*(uint *)(puVar10 + -0x7d54) & 0xfffffff0)
                                  );
              auVar47 = vectorConditionalSelect
                                  ((undefined1  [16])0x0,
                                   auVar50 ^ *(undefined1 (*) [16])
                                              (*(uint *)(puVar10 + -0x7d58) & 0xfffffff0),
                                   *(undefined1 (*) [16])(*(uint *)(puVar10 + -0x7d54) & 0xfffffff0)
                                  );
              local_2e0 = vectorConditionalSelect
                                    (auVar42,auVar50,
                                     *(undefined1 (*) [16])
                                      (*(uint *)(puVar10 + -0x7d50) & 0xfffffff0));
              local_2d0 = vectorConditionalSelect
                                    (auVar47,auVar57,
                                     *(undefined1 (*) [16])
                                      (*(uint *)(puVar10 + -0x7d50) & 0xfffffff0));
              puVar34 = (undefined4 *)(*(uint *)(puVar10 + -0x7f9c) & 0xfffffff0);
              uStack_2b0 = *puVar34;
              uStack_2ac = puVar34[1];
              uStack_2a8 = puVar34[2];
              uStack_2a4 = puVar34[3];
              auVar57 = *(undefined1 (*) [16])(*(uint *)(puVar10 + -0x7d4c) & 0xfffffff0);
              auVar42 = *(undefined1 (*) [16])(puVar7 + 0x8e);
              auVar50 = *(undefined1 (*) [16])(puVar7 + 0x90);
              auVar47 = *(undefined1 (*) [16])(*(uint *)(puVar10 + -0x7d48) & 0xfffffff0);
              auVar51 = vectorPermute(auVar42,auVar42,auVar57);
              auVar58 = vectorPermute(auVar42,auVar42,auVar47);
              auVar48 = vectorPermute(auVar50,auVar50,auVar57);
              auVar49 = vectorPermute(auVar50,auVar50,auVar47);
              auVar51 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar7 + 0x86),auVar51,
                                   (undefined1  [16])0x0);
              auVar65 = *(undefined1 (*) [16])(*(uint *)(puVar10 + -0x7d44) & 0xfffffff0);
              auVar58 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar7 + 0x88),auVar58,
                                   (undefined1  [16])0x0);
              auVar48 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar7 + 0x86),auVar48,
                                   (undefined1  [16])0x0);
              auVar59 = *(undefined1 (*) [16])(*(uint *)(puVar10 + -0x7d40) & 0xfffffff0);
              auVar49 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar7 + 0x88),auVar49,
                                   (undefined1  [16])0x0);
              auVar60 = vectorPermute(auVar42,auVar42,auVar65);
              auVar42 = vectorPermute(auVar42,auVar42,auVar59);
              auVar64 = vectorPermute(auVar50,auVar50,auVar65);
              auVar50 = vectorPermute(auVar50,auVar50,auVar59);
              auVar60 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar7 + 0x8a),auVar60,auVar51);
              auVar51 = vectorPermute(auVar66,auVar66,auVar57);
              auVar42 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar7 + 0x8c),auVar42,auVar58);
              auVar58 = vectorPermute(auVar66,auVar66,auVar47);
              auVar64 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar7 + 0x8a),auVar64,auVar48);
              auVar48 = vectorPermute(auVar66,auVar66,auVar65);
              auVar50 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar7 + 0x8c),auVar50,auVar49);
              in_vs37 = vectorPermute(auVar66,auVar66,auVar59);
              auVar42 = vectorAddFloatingPoint(auVar60,auVar42);
              vectorAddFloatingPoint(auVar64,auVar50);
              *(undefined1 (*) [16])lVar30 = auVar42;
              auVar42 = *(undefined1 (*) [16])(puVar7 + 0x92);
              auVar50 = vectorPermute(auVar42,auVar42,auVar57);
              auVar60 = vectorPermute(auVar42,auVar42,auVar47);
              auVar49 = vectorPermute(auVar42,auVar42,auVar59);
              auVar50 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar7 + 0x86),auVar50,
                                   (undefined1  [16])0x0);
              auVar42 = vectorPermute(auVar42,auVar42,auVar65);
              auVar60 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar7 + 0x88),auVar60,
                                   (undefined1  [16])0x0);
              auVar50 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar7 + 0x8a),auVar42,auVar50);
              auVar42 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar7 + 0x8c),auVar49,auVar60);
              vectorAddFloatingPoint(auVar50,auVar42);
              auVar42 = *(undefined1 (*) [16])(puVar7 + 0x94);
              auVar57 = vectorPermute(auVar42,auVar42,auVar57);
              auVar50 = vectorPermute(auVar42,auVar42,auVar47);
              auVar47 = vectorPermute(auVar42,auVar42,auVar59);
              auVar57 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar7 + 0x86),auVar57,
                                   (undefined1  [16])0x0);
              auVar42 = vectorPermute(auVar42,auVar42,auVar65);
              auVar50 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar7 + 0x88),auVar50,
                                   (undefined1  [16])0x0);
              auVar57 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar7 + 0x8a),auVar42,auVar57);
              auVar42 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar7 + 0x8c),auVar47,auVar50);
              vectorAddFloatingPoint(auVar57,auVar42);
              dVar39 = (double)(float)(dVar39 + (double)*(float *)(puVar10 + -0x7d3c));
              auVar42 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar7 + 0x80),auVar58,
                                   (undefined1  [16])0x0);
              auVar50 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar7 + 0x7e),auVar51,
                                   (undefined1  [16])0x0);
              auVar42 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar7 + 0x84),in_vs37,auVar42);
              auVar50 = vectorMultiplyAddFloatingPoint
                                  (*(undefined1 (*) [16])(puVar7 + 0x82),auVar48,auVar50);
              auVar42 = vectorAddFloatingPoint(auVar50,auVar42);
              *(undefined1 (*) [16])(puVar7 + 0x76) = auVar42;
              dVar41 = -(double)(float)((double)*(float *)(puVar7 + 0x76) * dVar40 - dVar40);
              fVar2 = (float)((double)*(float *)((int)puVar7 + 0x3b4) * dVar40 + dVar40);
            }
            uVar17 = *(undefined4 *)(iVar15 + 0x60);
            uVar11 = 0x40;
            dVar40 = (double)fVar2;
            while( true ) {
              iVar27 = _opd_FUN_00255710(uVar17,iVar16);
              if (iVar27 != 0) {
                fVar2 = *(float *)(puVar10 + -0x7d1c);
                uVar6 = *(uint *)(iVar27 + 0x18);
                puVar7[0xc2] = (ulonglong)*(uint *)(iVar27 + 0x10);
                uVar31 = puVar7[0xc2];
                puVar7[0xc2] = (ulonglong)uVar6;
                uVar38 = puVar7[0xc2];
                uVar6 = *(uint *)(iVar27 + 0x14);
                puVar7[0xc2] = (ulonglong)*(uint *)(iVar27 + 0x1c);
                uVar29 = puVar7[0xc2];
                puVar7[0xc2] = (ulonglong)uVar6;
                fVar3 = (float)((double)(longlong)uVar31 * dVar41 + (double)(longlong)uVar38) *
                        fVar2;
                fVar2 = (float)((double)(longlong)puVar7[0xc2] * dVar40 + (double)(longlong)uVar29)
                        * fVar2;
                if (fVar3 < *(float *)(puVar10 + -0x7fdc)) {
                  local_160 = (int)(fVar3 - *(float *)(puVar10 + -0x7d24));
                  uVar33 = *(undefined2 *)((int)puVar7 + 0x5b2);
                }
                else {
                  uVar33 = *(undefined2 *)((int)puVar7 + 0x5b2);
                }
                if (fVar2 < *(float *)(puVar10 + -0x7fdc)) {
                  local_160 = (int)(fVar2 - *(float *)(puVar10 + -0x7d24));
                  uVar35 = *(undefined2 *)((int)puVar7 + 0x5b2);
                }
                else {
                  uVar35 = *(undefined2 *)((int)puVar7 + 0x5b2);
                }
                _opd_FUN_00253ae8(iVar16,uVar33,uVar35,0,iVar18);
              }
              _opd_FUN_00253e60(iVar16,lVar30,0,0);
              uVar11 = uVar11 | *(uint *)(puVar7 + 0x5a) & 0xffffff00;
              *(uint *)(puVar7 + 0x5a) = uVar11;
              _opd_FUN_00253c60(iVar16,uVar11,0xf,0,iVar18);
              bVar1 = iVar18 == 0;
              iVar18 = iVar18 + -1;
              if (bVar1) goto LAB_00bda5f0;
              if (iVar18 != 0) break;
LAB_00bda554:
              uVar17 = *(undefined4 *)(iVar15 + 0x60);
              if (iVar16 == 0) {
                _opd_FUN_00253e60(0,lVar30,0,0);
                uVar11 = *(uint *)(puVar7 + 0x5a) & 0xffffff00 | 0x80;
                *(uint *)(puVar7 + 0x5a) = uVar11;
                _opd_FUN_00253c60(0,uVar11,0xf,0,0);
                goto LAB_00bda5f0;
              }
              dVar41 = (double)*(float *)(puVar10 + -0x7d24);
              uVar11 = 0x80;
              dVar40 = dVar41;
            }
          } while( true );
        }
LAB_00bda5f0:
        bVar1 = iVar20 == 0;
        iVar20 = iVar20 + -1;
        if (bVar1) break;
        iVar32 = iVar32 + 0xc;
        uVar29 = puVar7[0xb9];
      } while( true );
    }
  }
  _opd_FUN_00b30830(*(undefined4 *)(iVar15 + 0x60),0x1c090);
  _opd_FUN_0024d078((double)*(float *)(puVar10 + -0x7fdc),*(undefined4 *)(iVar15 + 0x60));
  if (*(int *)(iVar15 + 0x68) != 0) {
    _opd_FUN_0024d078((double)*(float *)(puVar10 + -0x7fdc),*(int *)(iVar15 + 0x68));
  }
  if (*(int *)(iVar15 + 0x6c) != 0) {
    _opd_FUN_0024d078((double)*(float *)(puVar10 + -0x7fdc),*(int *)(iVar15 + 0x6c));
  }
  **(int **)(puVar10 + -0x8000) = iVar15;
  return lVar14;
}

