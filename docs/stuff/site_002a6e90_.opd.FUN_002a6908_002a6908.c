// hook SITE 0x002a6e90

longlong _opd_FUN_002a6908(void)

{
  undefined4 uVar1;
  float fVar2;
  float fVar3;
  float fVar4;
  float fVar5;
  float fVar6;
  float fVar7;
  float fVar8;
  float fVar9;
  float fVar10;
  int *piVar11;
  uint uVar12;
  uint uVar13;
  float fVar14;
  int iVar15;
  undefined *puVar16;
  int iVar17;
  int iVar18;
  int iVar19;
  int iVar20;
  longlong lVar21;
  longlong lVar22;
  double dVar23;
  double dVar24;
  double dVar25;
  double dVar26;
  double dVar27;
  double dVar28;
  double dVar29;
  double dVar30;
  
  puVar16 = PTR_DAT_0121b02c;
  lVar21 = 0;
  iVar17 = _opd_FUN_000be778(0x220,0xb00000000000000);
  if (iVar17 != 0) {
    lVar21 = _opd_FUN_000bead8(iVar17,0xb00000000000000,*(undefined4 *)(puVar16 + -0x7fe8),
                               *(undefined4 *)(puVar16 + -0x7fe4));
    if (lVar21 == 0) {
      _opd_FUN_000c1e00(iVar17);
    }
    else {
      dVar27 = (double)*(float *)(puVar16 + -0x7f90);
      *(undefined4 *)(iVar17 + 0x60) = *(undefined4 *)(*(int *)(puVar16 + -0x7fe0) + 4);
      iVar18 = _opd_FUN_000d9020(0x4d8965);
      if (iVar18 != 0) {
        iVar18 = _opd_FUN_000db2f8();
        dVar27 = (double)((float)(longlong)iVar18 * *(float *)(puVar16 + -0x7ff8));
      }
      dVar24 = (double)*(float *)(puVar16 + -0x7f88);
      iVar18 = _opd_FUN_000d9020(&DAT_00c88965);
      if (iVar18 != 0) {
        iVar18 = _opd_FUN_000db2f8();
        dVar24 = (double)((float)(longlong)iVar18 * *(float *)(puVar16 + -0x7ff8));
      }
      dVar23 = (double)*(float *)(puVar16 + -0x7f80);
      iVar18 = _opd_FUN_000d9020(0xac922a);
      if (iVar18 != 0) {
        iVar18 = _opd_FUN_000db2f8();
        dVar23 = (double)((float)(longlong)iVar18 * *(float *)(puVar16 + -0x7ff8));
      }
      dVar26 = (double)*(float *)(puVar16 + -0x7f78);
      iVar18 = _opd_FUN_000d9020(&DAT_00fda1b8);
      if (iVar18 != 0) {
        iVar18 = _opd_FUN_000db2f8();
        dVar26 = (double)((float)(longlong)iVar18 * *(float *)(puVar16 + -0x7ff8));
      }
      dVar25 = (double)*(float *)(puVar16 + -0x7f70);
      iVar18 = _opd_FUN_000d9020(0x1c5200);
      if (iVar18 != 0) {
        iVar18 = _opd_FUN_000db2f8();
        dVar25 = (double)((float)(longlong)iVar18 * *(float *)(puVar16 + -0x7ff8));
      }
      dVar29 = (double)*(float *)(puVar16 + -0x7f78);
      iVar18 = _opd_FUN_000d9020(0x125201);
      if (iVar18 != 0) {
        iVar18 = _opd_FUN_000db2f8();
        dVar29 = (double)((float)(longlong)iVar18 * *(float *)(puVar16 + -0x7ff8));
      }
      dVar28 = (double)*(float *)(puVar16 + -0x7f80);
      iVar18 = _opd_FUN_000d9020(0x4fe994);
      if (iVar18 != 0) {
        iVar18 = _opd_FUN_000db2f8();
        dVar28 = (double)((float)(longlong)iVar18 * *(float *)(puVar16 + -0x7ff8));
      }
      dVar30 = (double)*(float *)(puVar16 + -0x7f78);
      iVar18 = _opd_FUN_000d9020(0x5207fc);
      if (iVar18 != 0) {
        iVar18 = _opd_FUN_000db2f8();
        dVar30 = (double)((float)(longlong)iVar18 * *(float *)(puVar16 + -0x7ff8));
      }
      iVar18 = _opd_FUN_000d9020(0x5206c6);
      if (iVar18 != 0) {
        iVar18 = _opd_FUN_000db2f8();
        dVar30 = (double)((float)(longlong)iVar18 * *(float *)(puVar16 + -0x7ff8));
      }
      iVar18 = _opd_FUN_000d9020(0x2c0b75);
      if (iVar18 != 0) {
        _opd_FUN_000db2f8();
      }
      piVar11 = *(int **)(puVar16 + -0x7ffc);
      if (*(longlong *)(piVar11 + 2) == 0) {
        _opd_FUN_002aec38(dVar27,iVar17 + 0x6c);
        _opd_FUN_002aec38(dVar24,iVar17 + 0x80);
        _opd_FUN_002aec38(dVar23,iVar17 + 0x94);
        _opd_FUN_002aec38(dVar26,iVar17 + 0xa8);
        _opd_FUN_002aec38(dVar25,iVar17 + 0xbc);
        _opd_FUN_002aec38(dVar29,iVar17 + 0xd0);
        _opd_FUN_002aec38(dVar28,iVar17 + 0xe4);
        _opd_FUN_002aec38(dVar30,iVar17 + 0xf8);
        iVar18 = *(int *)(puVar16 + -0x8000);
        uVar1 = *(undefined4 *)(puVar16 + -0x7fd8);
        fVar2 = *(float *)(puVar16 + -0x7f70);
        fVar3 = *(float *)(puVar16 + -0x7fb0);
        *(undefined4 *)(iVar17 + 0x218) = 2;
        *(undefined4 *)(iVar17 + 0x110) = uVar1;
        *(undefined4 *)(iVar17 + 0x10c) = uVar1;
        *(undefined4 *)(iVar17 + 0x21c) = 0;
        uVar12 = *(uint *)(iVar18 + 0x424);
        uVar13 = *(uint *)(iVar18 + 0x428);
        fVar4 = *(float *)(puVar16 + -0x7fb8);
        fVar5 = *(float *)(puVar16 + -0x7fa8);
        iVar18 = (uVar12 >> 0x17 & 0xff) - 0x7f;
        fVar6 = *(float *)(puVar16 + -0x7fc0);
        iVar19 = (uVar13 >> 0x17 & 0xff) - 0x7f;
        fVar7 = *(float *)(puVar16 + -0x7fa0);
        fVar14 = (float)(uVar12 + iVar18 * -0x800000) - fVar2;
        fVar2 = (float)(uVar13 + iVar19 * -0x800000) - fVar2;
        fVar8 = *(float *)(puVar16 + -0x7fc8);
        fVar9 = *(float *)(puVar16 + -0x7fd0);
        iVar20 = 0;
        lVar22 = 0x20;
        fVar10 = *(float *)(puVar16 + -0x7f98);
        do {
          iVar15 = iVar17 + iVar20;
          iVar20 = iVar20 + 4;
          *(float *)(iVar15 + 0x194) =
               fVar2 * fVar2 * fVar2 * fVar2 *
               (fVar2 * (fVar2 * (fVar2 * (fVar2 * fVar4 + fVar3) - fVar5) + fVar7) - fVar10) +
               fVar2 * (fVar2 * (fVar2 * fVar9 - fVar8) + fVar6) + (float)(longlong)iVar19;
          *(float *)(iVar15 + 0x114) =
               fVar14 * fVar14 * fVar14 * fVar14 *
               (fVar14 * (fVar14 * (fVar14 * (fVar14 * fVar4 + fVar3) - fVar5) + fVar7) - fVar10) +
               fVar14 * (fVar14 * (fVar14 * fVar9 - fVar8) + fVar6) + (float)(longlong)iVar18;
          lVar22 = lVar22 + -1;
        } while (lVar22 != 0);
        *(undefined4 *)(iVar17 + 0x34) = *(undefined4 *)(puVar16 + -0x7fdc);
        *(undefined4 *)(iVar17 + 0x214) = 0;
        if (*piVar11 == 0) {
          *piVar11 = iVar17;
          *(undefined8 *)(piVar11 + 2) = *(undefined8 *)(iVar17 + 0x20);
        }
      }
      else {
        *(undefined4 *)(iVar17 + 0x34) = *(undefined4 *)(puVar16 + -0x7fdc);
        *(undefined4 *)(iVar17 + 0x68) = 1;
        iVar17 = *piVar11;
        _opd_FUN_002aec38(dVar27,iVar17 + 0x6c);
        _opd_FUN_002aec38(dVar24,iVar17 + 0x80);
        _opd_FUN_002aec38(dVar23,iVar17 + 0x94);
        _opd_FUN_002aec38(dVar26,iVar17 + 0xa8);
        _opd_FUN_002aec38(dVar25,iVar17 + 0xbc);
        _opd_FUN_002aec38(dVar29,iVar17 + 0xd0);
        _opd_FUN_002aec38(dVar28,iVar17 + 0xe4);
        _opd_FUN_002aec38(dVar30,iVar17 + 0xf8);
      }
    }
  }
  return lVar21;
}

