/* containing function of static patch target(s); entry .opd.FUN_002bc0e0 @ 002bc0e0 */

longlong _opd_FUN_002bc0e0(void)

{
  float fVar1;
  uint uVar2;
  undefined4 *puVar3;
  undefined *puVar4;
  int iVar6;
  int iVar7;
  int iVar8;
  int iVar9;
  int iVar10;
  int iVar11;
  int iVar12;
  int iVar13;
  int iVar14;
  int iVar15;
  int iVar16;
  longlong lVar5;
  undefined4 uVar17;
  undefined4 uVar18;
  int iVar19;
  int iVar20;
  double dVar21;
  double dVar22;
  double dVar23;
  double dVar24;
  double dVar25;
  double dVar26;
  double dVar27;
  double dVar28;
  double dVar29;
  undefined4 local_150;
  undefined4 uStack_14c;
  undefined4 local_148;
  undefined4 uStack_144;
  undefined4 local_140;
  undefined4 uStack_13c;
  undefined4 local_138;
  undefined4 uStack_134;
  undefined4 local_130;
  undefined4 uStack_12c;
  undefined4 local_128;
  undefined4 uStack_124;
  undefined4 local_f0;
  undefined4 uStack_ec;
  undefined4 local_e8;
  undefined4 uStack_e4;
  
  puVar4 = PTR_PTR_0121b07c;
  iVar6 = _opd_FUN_000dc9f0(0x32bf12,0xfa);
  iVar7 = _opd_FUN_000dc9f0(0x5a9773,10);
  iVar8 = _opd_FUN_000dc9f0(0x94f3fe,0);
  iVar9 = _opd_FUN_000dc9f0(0x26fbfe,1);
  iVar10 = _opd_FUN_000dc9f0(0x84c54a,0x19);
  iVar11 = _opd_FUN_000dc9f0(0x26751c,800);
  iVar12 = _opd_FUN_000dc9f0(0x4819f0,10);
  iVar13 = _opd_FUN_000dc9f0(0x7c6012,1000);
  iVar14 = _opd_FUN_000dc9f0(0xc6375e,0x5dc);
  iVar15 = _opd_FUN_000dc9f0(0x2400ab,0x32);
  iVar16 = _opd_FUN_000be778(0x2e0,0xb00000000000000);
  if (iVar16 != 0) {
    lVar5 = _opd_FUN_000bead8(iVar16,0xb00000000000000,*(undefined4 *)(puVar4 + -0x8000),
                              *(undefined4 *)(puVar4 + -0x7ffc));
    if (lVar5 != 0) {
      *(undefined4 *)(iVar16 + 0x60) = *(undefined4 *)(*(int *)(puVar4 + -0x7ff8) + 4);
      uVar17 = _opd_FUN_000fe608(0xc908ec);
      uVar18 = *(undefined4 *)(puVar4 + -0x7ff4);
      *(undefined4 *)(iVar16 + 0x130) = uVar17;
      *(undefined2 *)(iVar16 + 0x14e) = 1;
      *(undefined4 *)(iVar16 + 0x1d0) = uVar18;
      uVar18 = _opd_FUN_000fe608(0xee2587);
      *(undefined4 *)(iVar16 + 0x200) = uVar18;
      iVar19 = _opd_FUN_00131028(0xe1,2,0x80);
      *(int *)(iVar16 + 100) = iVar19;
      if (iVar19 != 0) {
        _opd_FUN_00130f70(iVar19,0);
        iVar20 = _opd_FUN_000bc1b0(0xffffffffffffffff,0xc0,0);
        *(int *)(iVar16 + 0x68) = iVar20;
        if (iVar20 != 0) {
          dVar29 = (double)*(float *)(puVar4 + -0x7fa0);
          dVar26 = (double)*(float *)(puVar4 + -0x7f4c);
          dVar25 = (double)(float)((double)(longlong)iVar11 * dVar29);
          dVar24 = (double)(longlong)iVar8;
          dVar23 = (double)(longlong)iVar9;
          dVar27 = (double)(float)((double)(longlong)iVar15 * dVar29);
          dVar22 = (double)(longlong)iVar13;
          dVar28 = (double)(float)(dVar26 / (double)(longlong)iVar12);
          dVar21 = (double)(longlong)iVar14;
          _opd_FUN_000d60b8(iVar20,0xc0);
          fVar1 = *(float *)(puVar4 + -0x7fcc);
          *(int *)(iVar19 + 0x2c) = iVar20;
          _opd_FUN_00130020((double)fVar1,(double)fVar1,iVar20);
          _opd_FUN_001301d0(iVar20 + 0xc,0,0,0,0x100);
          _opd_FUN_00130330(iVar20 + 0x18,0x80808080);
          _opd_FUN_001300a0(iVar20 + 0x20,0x222);
          _opd_FUN_0012fff8((double)*(float *)(puVar4 + -0x7fc4),
                            (double)*(float *)(puVar4 + -0x7fc4),
                            (double)*(float *)(puVar4 + -0x7fbc),
                            (double)*(float *)(puVar4 + -0x7fbc),iVar20 + 0x28);
          _opd_FUN_001301b0(iVar20 + 0x3c,0xffffffffffffffff);
          _opd_FUN_00130170(iVar20 + 0x44,0);
          iVar8 = *(int *)(puVar4 + -0x7ff0);
          uVar2 = *(uint *)(iVar8 + 0x360);
          _opd_FUN_001304d8(iVar20 + 0x4c,0,0,0x200,0x200,*(int *)(iVar8 + 0x350),
                            *(undefined4 *)(iVar8 + 0x354),
                            *(int *)(iVar8 + 0x350) +
                            ((int)uVar2 >> 1) + (uint)((int)uVar2 < 0 && (uVar2 & 1) != 0));
          _opd_FUN_001300a0(iVar20 + 0x6c,0);
          _opd_FUN_0012fff8((double)*(float *)(puVar4 + -0x7fc4),
                            (double)*(float *)(puVar4 + -0x7fc4),
                            (double)*(float *)(puVar4 + -0x7fb4),
                            (double)*(float *)(puVar4 + -0x7fac),iVar20 + 0x74);
          _opd_FUN_0012ffd0(iVar20 + 0x88,0,0,*(undefined4 *)(iVar8 + 0x360),
                            *(undefined4 *)(iVar8 + 0x364));
          _opd_FUN_001301b0(iVar20 + 0x9c,0);
          _opd_FUN_00130488(iVar20 + 0x94,iVar16 + 0x140);
          _opd_FUN_00130170(iVar20 + 100,0x222);
          _opd_FUN_001304d8(iVar20 + 0xa4,0,0,0x400,0x300,0,0,0x200);
          _opd_FUN_001300c0(iVar20 + 0xbc);
          local_150 = (undefined4)((ulonglong)*(undefined8 *)(iVar8 + 0x120) >> 0x20);
          uStack_14c = (undefined4)*(undefined8 *)(iVar8 + 0x120);
          local_148 = (undefined4)((ulonglong)*(undefined8 *)(iVar8 + 0x128) >> 0x20);
          uStack_144 = (undefined4)*(undefined8 *)(iVar8 + 0x128);
          local_f0 = (undefined4)((ulonglong)*(undefined8 *)(iVar8 + 0x130) >> 0x20);
          uStack_ec = (undefined4)*(undefined8 *)(iVar8 + 0x130);
          local_e8 = (undefined4)((ulonglong)*(undefined8 *)(iVar8 + 0x138) >> 0x20);
          uStack_e4 = (undefined4)*(undefined8 *)(iVar8 + 0x138);
          local_130 = (undefined4)((ulonglong)*(undefined8 *)(iVar8 + 0x100) >> 0x20);
          uStack_12c = (undefined4)*(undefined8 *)(iVar8 + 0x100);
          local_128 = (undefined4)((ulonglong)*(undefined8 *)(iVar8 + 0x108) >> 0x20);
          uStack_124 = (undefined4)*(undefined8 *)(iVar8 + 0x108);
          local_140 = (undefined4)((ulonglong)*(undefined8 *)(iVar8 + 0x110) >> 0x20);
          uStack_13c = (undefined4)*(undefined8 *)(iVar8 + 0x110);
          local_138 = (undefined4)((ulonglong)*(undefined8 *)(iVar8 + 0x118) >> 0x20);
          uStack_134 = (undefined4)*(undefined8 *)(iVar8 + 0x118);
          puVar3 = (undefined4 *)(iVar16 + 0x250U & 0xfffffff0);
          *puVar3 = local_f0;
          puVar3[1] = uStack_ec;
          puVar3[2] = local_e8;
          puVar3[3] = uStack_e4;
          puVar3 = (undefined4 *)(iVar16 + 0x220U & 0xfffffff0);
          *puVar3 = local_130;
          puVar3[1] = uStack_12c;
          puVar3[2] = local_128;
          puVar3[3] = uStack_124;
          puVar3 = (undefined4 *)(iVar16 + 0x230U & 0xfffffff0);
          *puVar3 = local_140;
          puVar3[1] = uStack_13c;
          puVar3[2] = local_138;
          puVar3[3] = uStack_134;
          puVar3 = (undefined4 *)(iVar16 + 0x240U & 0xfffffff0);
          *puVar3 = local_150;
          puVar3[1] = uStack_14c;
          puVar3[2] = local_148;
          puVar3[3] = uStack_144;
          puVar3 = (undefined4 *)(iVar16 + 0x260U & 0xfffffff0);
          *puVar3 = local_130;
          puVar3[1] = uStack_12c;
          puVar3[2] = local_128;
          puVar3[3] = uStack_124;
          puVar3 = (undefined4 *)(iVar16 + 0x270U & 0xfffffff0);
          *puVar3 = local_140;
          puVar3[1] = uStack_13c;
          puVar3[2] = local_138;
          puVar3[3] = uStack_134;
          puVar3 = (undefined4 *)(iVar16 + 0x290U & 0xfffffff0);
          *puVar3 = local_f0;
          puVar3[1] = uStack_ec;
          puVar3[2] = local_e8;
          puVar3[3] = uStack_e4;
          puVar3 = (undefined4 *)(iVar16 + 0x280U & 0xfffffff0);
          *puVar3 = local_150;
          puVar3[1] = uStack_14c;
          puVar3[2] = local_148;
          puVar3[3] = uStack_144;
          *(float *)(iVar16 + 0x2d8) = (float)dVar23;
          *(float *)(iVar16 + 0x2c4) = (float)dVar21;
          *(float *)(iVar16 + 0x2c0) = (float)dVar22;
          *(float *)(iVar16 + 0x2dc) = (float)dVar27;
          *(float *)(iVar16 + 0x2d0) = (float)dVar28;
          *(float *)(iVar16 + 0x2cc) = (float)dVar25;
          *(float *)(iVar16 + 0x2d4) = (float)dVar24;
          *(float *)(iVar16 + 0x2b0) = (float)dVar23;
          *(float *)(iVar16 + 0x2ac) = (float)dVar24;
          *(float *)(iVar16 + 0x2c8) = (float)dVar27;
          *(float *)(iVar16 + 700) = (float)dVar28;
          *(float *)(iVar16 + 0x2b8) = (float)dVar25;
          *(float *)(iVar16 + 0x2a8) = (float)(dVar26 / (double)(longlong)iVar7);
          *(float *)(iVar16 + 0x2b4) = (float)((double)(longlong)iVar10 * dVar29);
          *(float *)(iVar16 + 0x2a4) = (float)((double)(longlong)iVar6 * dVar29);
          return lVar5;
        }
      }
    }
    _opd_FUN_000c1e00(iVar16);
  }
  return 0;
}

