// hook SITE 0x000f9de4

/* WARNING: Heritage AFTER dead removal. Example location: w0xfffff5f0 : 0x000fa304 */
/* WARNING: Type propagation algorithm not settling */
/* WARNING: Restarted to delay deadcode elimination for space: stack */

void _opd_FUN_000f9598(ulonglong param_1)

{
  bool bVar1;
  float fVar2;
  float fVar3;
  float fVar4;
  undefined4 uVar5;
  float fVar6;
  float fVar7;
  undefined4 uVar8;
  float fVar9;
  undefined4 uVar10;
  undefined4 uVar11;
  float fVar12;
  float fVar13;
  float fVar14;
  float fVar15;
  float fVar16;
  float fVar17;
  float fVar18;
  float fVar19;
  float fVar20;
  float fVar21;
  float fVar22;
  float fVar23;
  float fVar24;
  float fVar25;
  float fVar26;
  float fVar27;
  float fVar28;
  float fVar29;
  float fVar30;
  float fVar31;
  float fVar32;
  float fVar33;
  int iVar34;
  uint uVar35;
  undefined4 uVar36;
  int iVar37;
  uint uVar38;
  uint uVar39;
  uint uVar40;
  uint uVar41;
  uint uVar42;
  uint uVar43;
  uint uVar44;
  uint uVar45;
  uint uVar46;
  uint uVar47;
  undefined4 uVar48;
  int *piVar49;
  uint uVar50;
  undefined *puVar51;
  uint uVar52;
  int iVar53;
  undefined1 (*pauVar54) [16];
  ulonglong uVar55;
  longlong lVar56;
  float *pfVar57;
  ulonglong uVar58;
  float fVar59;
  undefined4 *puVar60;
  float *pfVar61;
  longlong lVar62;
  int iVar63;
  int iVar64;
  ulonglong uVar65;
  ulonglong uVar66;
  undefined1 (*pauVar67) [16];
  ulonglong uVar68;
  ulonglong uVar69;
  byte in_cr0;
  byte in_cr1;
  byte in_cr2;
  byte in_cr3;
  byte unaff_cr4;
  byte in_cr5;
  byte in_cr6;
  byte in_cr7;
  longlong lVar70;
  double dVar71;
  double dVar72;
  double dVar73;
  double dVar74;
  double dVar75;
  undefined1 auVar76 [16];
  undefined1 auVar77 [16];
  undefined1 auVar78 [16];
  undefined1 auVar79 [16];
  undefined1 auVar80 [16];
  int iVar81;
  int iVar82;
  int iVar83;
  undefined1 auVar84 [16];
  undefined1 auVar85 [16];
  undefined1 auVar86 [16];
  undefined1 auVar87 [16];
  undefined1 auVar88 [16];
  undefined1 auVar89 [16];
  undefined1 auVar90 [16];
  undefined1 auVar91 [16];
  undefined1 auVar92 [16];
  undefined1 auVar93 [16];
  undefined1 auVar94 [16];
  undefined1 auVar95 [16];
  undefined1 auVar96 [16];
  undefined1 auVar97 [16];
  uint uStack00000008;
  float local_a10;
  float local_a0c;
  undefined4 local_a08;
  undefined4 local_a04;
  undefined1 auStack_9fc [12];
  undefined1 auStack_9f0 [16];
  float local_9e0;
  float local_9dc;
  undefined4 local_9d8;
  undefined4 local_9d4;
  undefined1 auStack_9cc [12];
  undefined1 auStack_9c0 [16];
  undefined1 auStack_9b0 [16];
  undefined1 auStack_99c [12];
  undefined1 local_990 [16];
  float local_980;
  float local_97c;
  undefined4 local_978;
  undefined4 local_974;
  undefined1 local_180 [16];
  ulonglong local_170;
  float local_168;
  
  puVar51 = PTR_PTR_0121ad00;
  uStack00000008 =
       (uint)(in_cr0 & 0xf) << 0x1c | (uint)(in_cr1 & 0xf) << 0x18 | (uint)(in_cr2 & 0xf) << 0x14 |
       (uint)(in_cr3 & 0xf) << 0x10 | (uint)(unaff_cr4 & 0xf) << 0xc | (uint)(in_cr5 & 0xf) << 8 |
       (uint)(in_cr6 & 0xf) << 4 | (uint)(in_cr7 & 0xf);
  iVar34 = **(int **)(PTR_PTR_0121ad00 + -0x8000);
  while (iVar64 = iVar34, iVar34 != 0) {
    while (uVar35 = *(uint *)(iVar64 + 0x394), (uVar35 & 1) != 0) {
      iVar34 = *(int *)(puVar51 + -0x7ebc);
      auVar97 = vectorSplatImmediateSignedByte(0);
      dVar74 = (double)*(float *)(puVar51 + -0x7fe4);
      iVar63 = (int)param_1;
      uVar36 = *(undefined4 *)(iVar34 + 0x248);
      _opd_FUN_000e73e8();
      iVar37 = *(int *)(puVar51 + -0x7fd8);
      uVar55 = *(ulonglong *)(iVar34 + 0x40) >> 0x21 & 0x7ff;
      **(undefined4 **)(iVar37 + 8) = 0x41800;
      *(uint *)(*(int *)(iVar37 + 8) + 4) = (uint)(uVar55 << 4) | 0x1000000;
      iVar53 = *(int *)(iVar34 + 0x248);
      iVar81 = *(int *)(iVar37 + 8);
      *(ulonglong *)(iVar34 + 0x40) =
           ((*(ulonglong *)(iVar34 + 0x40) >> 0x21 & 0x7ff) + 1 & 0x7ff) << 0x21 |
           *(ulonglong *)(iVar34 + 0x40) & 0xfffff001ffffffff;
      *(int *)(iVar34 + 0x248) = iVar53 + 1;
      *(short *)(iVar53 * 2 + iVar34 + 0x24c) = (short)uVar55;
      *(int *)(iVar37 + 8) = iVar81 + 8;
      *(undefined4 *)(iVar64 + 0x1518) = uVar36;
      _opd_FUN_001077d0();
      fVar4 = *(float *)(iVar64 + 0x3f4);
      dVar72 = (double)(*(float *)(iVar64 + 0x3f8) - fVar4);
      dVar71 = (double)(float)(1.0 / dVar72);
      *(undefined4 *)(iVar64 + 0x3f0) = *(undefined4 *)(iVar64 + 0x404);
      uVar36 = *(undefined4 *)(iVar64 + 0x400);
      uVar5 = *(undefined4 *)(iVar64 + 0x3fc);
      dVar71 = (double)(float)(dVar71 * -(double)(float)(dVar72 * dVar71 - dVar74) + dVar71);
      fVar2 = (float)(dVar71 * -(double)(float)(dVar72 * dVar71 - dVar74) + dVar71);
      **(undefined4 **)(iVar37 + 8) = 0x141efc;
      *(undefined4 *)(*(int *)(iVar37 + 8) + 4) = 0x17;
      iVar53 = *(int *)(iVar37 + 8);
      *(float **)(iVar37 + 8) = (float *)(iVar53 + 8);
      *(float *)(iVar53 + 8) = local_9e0;
      *(float *)(iVar53 + 0xc) = local_9dc;
      *(undefined4 *)(iVar53 + 0x10) = local_9d8;
      *(undefined4 *)(iVar53 + 0x14) = local_9d4;
      *(int *)(iVar37 + 8) = *(int *)(iVar37 + 8) + 0x10;
      _opd_FUN_0014f290(0x15,&local_9e0);
      uVar38 = *(uint *)(puVar51 + -0x7db0);
      uVar39 = *(uint *)(puVar51 + -0x7dac);
      auVar76._0_4_ = *(uint *)(iVar64 + 0x404) | 0xff;
      auVar76._4_12_ = auStack_9fc;
      auVar76 = vectorPermute(auVar76,auVar97,*(undefined1 (*) [16])(uVar38 & 0xfffffff0));
      auVar76 = vectorConvertFromUnsignedFixedPointWord(auVar76,0);
      auStack_9f0 = vectorMultiplyAddFloatingPoint
                              (auVar76,*(undefined1 (*) [16])(uVar39 & 0xfffffff0),auVar97);
      _opd_FUN_0014f290(0x16,auStack_9f0);
      _opd_FUN_00154298(iVar64,iVar63);
      _opd_FUN_000f5168((double)*(float *)(puVar51 + -0x7fd4),(double)*(float *)(puVar51 + -0x7fd4),
                        iVar64,iVar63);
      uVar40 = *(uint *)(iVar64 + 0x3a8);
      if ((*(uint *)(iVar64 + 0x394) & 0x10) == 0) {
        _opd_FUN_00105f10();
        fVar6 = *(float *)(puVar51 + -0x7fd4);
        *(byte *)(iVar34 + 0x40) = *(byte *)(iVar34 + 0x40) & 0xfe;
        *(ulonglong *)(iVar34 + 0x40) = *(ulonglong *)(iVar34 + 0x40) & 0xff0fffffffffffff;
        iVar53 = *(int *)(iVar64 + 0x364);
        iVar81 = *(int *)(iVar64 + 0x350);
        iVar82 = (*(int *)(iVar34 + 0x14) - *(int *)(iVar64 + 0x354)) - iVar53;
        uVar65 = (ulonglong)iVar81;
        uVar66 = (ulonglong)iVar82;
        uVar42 = *(uint *)(iVar64 + 0x360) & 0x7fff;
        _opd_FUN_000f14f0((double)fVar6,dVar74,uVar65,uVar66,uVar42,iVar53);
        uVar52 = *(uint *)(iVar34 + 0x10);
        uVar41 = *(uint *)(iVar34 + 0x14);
        uVar68 = (longlong)(int)(iVar81 + uVar42) - (ulonglong)uVar52;
        uVar69 = (longlong)(iVar53 + iVar82) - (ulonglong)uVar41;
        uVar58 = uVar65 & ~((longlong)uVar65 >> 0x3f);
        **(undefined4 **)(iVar37 + 8) = 0x808c0;
        uVar55 = uVar66 & ~((longlong)uVar66 >> 0x3f);
        *(uint *)(*(int *)(iVar37 + 8) + 4) =
             (uint)uVar58 & 0xffff |
             (uint)((((ulonglong)uVar52 + (uVar68 & (longlong)uVar68 >> 0x3f)) - uVar58 & 0xffffffff
                    ) << 0x10);
        *(uint *)(*(int *)(iVar37 + 8) + 8) =
             (uint)uVar55 & 0xffff |
             (uint)((((ulonglong)uVar41 + (uVar69 & (longlong)uVar69 >> 0x3f)) - uVar55 & 0xffffffff
                    ) << 0x10);
        *(int *)(iVar37 + 8) = *(int *)(iVar37 + 8) + 0xc;
        local_170 = uVar66;
        _opd_FUN_000ffbb8((double)(longlong)(int)uVar42,(double)(longlong)iVar53,
                          (double)(longlong)uVar65,(double)(longlong)uVar66);
        iVar53 = *(int *)(iVar64 + (int)((param_1 & 0xffffffff) << 2) + 0x46c);
        if (iVar53 != 0) {
          iVar53 = _opd_FUN_000e9188(iVar53);
          **(uint **)(iVar37 + 8) = *(uint *)(iVar53 + 4) | 2;
          *(int *)(iVar37 + 8) = *(int *)(iVar37 + 8) + 4;
        }
        *(float *)(iVar64 + 0x424) = (float)dVar74;
        *(undefined4 *)(iVar64 + 0x428) = 0x3f000000;
      }
      else {
        _opd_FUN_001066a0();
        fVar6 = *(float *)(puVar51 + -0x7fd4);
        *(byte *)(iVar34 + 0x40) = *(byte *)(iVar34 + 0x40) | 1;
        *(ulonglong *)(iVar34 + 0x40) =
             *(ulonglong *)(iVar34 + 0x40) & 0xff0fffffffffffff | 0x10000000000000;
        iVar53 = *(int *)(iVar64 + 0x364);
        iVar81 = *(int *)(iVar64 + 0x350);
        iVar82 = (*(int *)(iVar34 + 0x14) - *(int *)(iVar64 + 0x354)) - iVar53;
        uVar65 = (ulonglong)iVar81;
        uVar66 = (ulonglong)iVar82;
        uVar42 = *(uint *)(iVar64 + 0x360) & 0x7fff;
        _opd_FUN_000f14f0((double)fVar6,dVar74,uVar65,uVar66,uVar42,iVar53);
        uVar52 = *(uint *)(iVar34 + 0x10);
        uVar41 = *(uint *)(iVar34 + 0x14);
        uVar68 = (longlong)(int)(iVar81 + uVar42) - (ulonglong)uVar52;
        uVar69 = (longlong)(iVar53 + iVar82) - (ulonglong)uVar41;
        uVar58 = uVar65 & ~((longlong)uVar65 >> 0x3f);
        **(undefined4 **)(iVar37 + 8) = 0x808c0;
        uVar55 = uVar66 & ~((longlong)uVar66 >> 0x3f);
        *(uint *)(*(int *)(iVar37 + 8) + 4) =
             (uint)uVar58 & 0xffff |
             (uint)((((ulonglong)uVar52 + (uVar68 & (longlong)uVar68 >> 0x3f)) - uVar58 & 0xffffffff
                    ) << 0x10);
        *(uint *)(*(int *)(iVar37 + 8) + 8) =
             (uint)uVar55 & 0xffff |
             (uint)((((ulonglong)uVar41 + (uVar69 & (longlong)uVar69 >> 0x3f)) - uVar55 & 0xffffffff
                    ) << 0x10);
        *(int *)(iVar37 + 8) = *(int *)(iVar37 + 8) + 0xc;
        local_170 = uVar66;
        _opd_FUN_000ffbb8((double)(longlong)(int)uVar42,(double)(longlong)iVar53,
                          (double)(longlong)uVar65,(double)(longlong)uVar66);
        iVar53 = *(int *)(iVar64 + (int)((param_1 & 0xffffffff) << 2) + 0x46c);
        if (iVar53 != 0) {
          iVar53 = _opd_FUN_000e9188(iVar53);
          **(uint **)(iVar37 + 8) = *(uint *)(iVar53 + 4) | 2;
          *(int *)(iVar37 + 8) = *(int *)(iVar37 + 8) + 4;
        }
        if ((*(uint *)(iVar64 + 0x468) == 0xffffffff) || (**(int **)(puVar51 + -0x7da8) != 0)) {
          lVar62 = 0x20;
          puVar60 = (undefined4 *)(iVar64 + 0x504);
          do {
            puVar60[0xf] = 0x3f800000;
            *puVar60 = 0x3f000000;
            puVar60[1] = 0x3f800000;
            puVar60[2] = 0x3f000000;
            puVar60[3] = 0x3f800000;
            puVar60[4] = 0x3f000000;
            puVar60[5] = 0x3f800000;
            puVar60[6] = 0x3f000000;
            puVar60[7] = 0x3f800000;
            puVar60[8] = 0x3f000000;
            puVar60[9] = 0x3f800000;
            puVar60[10] = 0x3f000000;
            puVar60[0xb] = 0x3f800000;
            puVar60[0xc] = 0x3f000000;
            puVar60[0xd] = 0x3f800000;
            puVar60[0xe] = 0x3f000000;
            lVar62 = lVar62 + -1;
            puVar60 = puVar60 + 0x10;
          } while (lVar62 != 0);
        }
        else {
          auVar76 = ZEXT416(0xd);
          auVar76 = auVar76 | auVar76 << 0x20 | auVar76 << 0x40 | auVar76 << 0x60;
          uVar52 = *(uint *)(puVar51 + -0x7da4);
          uVar41 = *(uint *)(puVar51 + -0x7da0);
          uVar42 = *(uint *)(puVar51 + -0x7d9c);
          uVar43 = *(uint *)(puVar51 + -0x7d98);
          uVar44 = *(uint *)(puVar51 + -0x7d94);
          uVar45 = *(uint *)(puVar51 + -0x7d90);
          uVar46 = *(uint *)(puVar51 + -0x7d8c);
          uVar47 = *(uint *)(puVar51 + -0x7d88);
          lVar62 = (param_1 + ((ulonglong)*(uint *)(iVar64 + 0x468) & 0x7fffffff) * 2 & 0x7ffff) *
                   0x2000 + (ulonglong)
                            **(uint **)(*(int *)(puVar51 + -0x7ff4) + iVar63 * 0x5c + 0x480);
          pauVar67 = (undefined1 (*) [16])&local_980;
          while( true ) {
            auVar80 = *(undefined1 (*) [16])(uVar43 & 0xfffffff0);
            piVar49 = (int *)(uVar45 & 0xfffffff0);
            iVar53 = *piVar49;
            iVar81 = piVar49[1];
            iVar82 = piVar49[2];
            iVar83 = piVar49[3];
            lVar70 = 8;
            lVar56 = lVar62;
            pauVar54 = pauVar67;
            do {
              uVar50 = (uint)lVar56;
              auVar91 = *(undefined1 (*) [16])(uVar42 & 0xfffffff0);
              auVar86 = *(undefined1 (*) [16])(uVar44 & 0xfffffff0);
              auVar84 = ZEXT416(8);
              auVar84 = auVar84 | auVar84 << 0x20 | auVar84 << 0x40 | auVar84 << 0x60;
              auVar96 = *(undefined1 (*) [16])(uVar50 + 0x10 & 0xfffffff0);
              auVar79._0_4_ = auVar84._0_4_ * 2;
              auVar79._4_4_ = auVar84._4_4_ * 2;
              auVar79._12_4_ = auVar84._12_4_;
              auVar79._8_4_ = auVar84._8_4_ * 2;
              auVar92._0_12_ = auVar79._0_12_;
              auVar92._12_4_ = auVar79._12_4_ * 2;
              auVar88 = *(undefined1 (*) [16])(uVar50 + 0x100 & 0xfffffff0);
              lVar56 = lVar56 + 0x20;
              auVar79 = *(undefined1 (*) [16])(uVar50 + 0x110 & 0xfffffff0);
              auVar77 = vectorPermute(auVar88,auVar79,*(undefined1 (*) [16])(uVar52 & 0xfffffff0));
              auVar84 = vectorPermute(*(undefined1 (*) [16])(uVar50 & 0xfffffff0),auVar96,
                                      *(undefined1 (*) [16])(uVar52 & 0xfffffff0));
              auVar85 = vectorLogicalAnd(auVar77,auVar86);
              auVar89 = vectorLogicalAnd(auVar84,auVar91);
              auVar93 = vectorLogicalAnd(auVar84,auVar86);
              auVar95 = vectorLogicalAnd(auVar77,auVar91);
              auVar84 = vectorLogicalAnd(auVar84,auVar80);
              auVar78 = vectorLogicalAnd(auVar77,auVar80);
              auVar94 = vectorShiftLeftIntegerWord(auVar93,auVar76);
              auVar90 = vectorShiftLeftIntegerWord(auVar89,auVar92);
              auVar77._0_4_ = auVar84._0_4_ + iVar53;
              auVar77._8_8_ = auVar84._8_8_;
              auVar77._4_4_ = auVar84._4_4_ + iVar81;
              auVar89._0_8_ = auVar77._0_8_;
              auVar89._12_4_ = auVar84._12_4_;
              auVar89._8_4_ = auVar84._8_4_ + iVar82;
              auVar84._0_12_ = auVar89._0_12_;
              auVar84._12_4_ = auVar89._12_4_ + iVar83;
              auVar93._0_4_ = auVar78._0_4_ + iVar53;
              auVar93._8_8_ = auVar78._8_8_;
              auVar93._4_4_ = auVar78._4_4_ + iVar81;
              auVar87._0_8_ = auVar93._0_8_;
              auVar87._12_4_ = auVar78._12_4_;
              auVar87._8_4_ = auVar78._8_4_ + iVar82;
              auVar78._0_12_ = auVar87._0_12_;
              auVar78._12_4_ = auVar87._12_4_ + iVar83;
              auVar95 = vectorShiftLeftIntegerWord(auVar95,auVar92);
              auVar93 = vectorShiftLeftIntegerWord(auVar85,auVar76);
              auVar84 = vectorShiftLeftIntegerWord(auVar84,auVar76);
              auVar89 = vectorShiftLeftIntegerWord(auVar78,auVar76);
              auVar79 = vectorPermute(auVar88,auVar79,*(undefined1 (*) [16])(uVar41 & 0xfffffff0));
              auVar85 = vectorPermute(*(undefined1 (*) [16])(uVar50 & 0xfffffff0),auVar96,
                                      *(undefined1 (*) [16])(uVar41 & 0xfffffff0));
              auVar77 = vectorLogicalAnd(auVar79,auVar91);
              auVar91 = vectorLogicalAnd(auVar85,auVar91);
              auVar78 = vectorLogicalAnd(auVar79,auVar86);
              auVar87 = vectorLogicalAnd(auVar85,auVar86);
              auVar95 = vectorAddFloatingPoint
                                  (auVar90 | auVar94 | auVar84,auVar95 | auVar93 | auVar89);
              auVar93 = vectorLogicalAnd(auVar79,auVar80);
              auVar79 = vectorLogicalAnd(auVar85,auVar80);
              auVar89 = vectorShiftLeftIntegerWord(auVar78,auVar76);
              auVar84 = vectorShiftLeftIntegerWord(auVar77,auVar92);
              auVar85._0_4_ = auVar93._0_4_ + iVar53;
              auVar85._8_8_ = auVar93._8_8_;
              auVar85._4_4_ = auVar93._4_4_ + iVar81;
              auVar86._0_8_ = auVar85._0_8_;
              auVar86._12_4_ = auVar93._12_4_;
              auVar86._8_4_ = auVar93._8_4_ + iVar82;
              auVar88._0_12_ = auVar86._0_12_;
              auVar88._12_4_ = auVar86._12_4_ + iVar83;
              auVar92 = vectorShiftLeftIntegerWord(auVar91,auVar92);
              auVar90._0_4_ = auVar79._0_4_ + iVar53;
              auVar90._8_8_ = auVar79._8_8_;
              auVar90._4_4_ = auVar79._4_4_ + iVar81;
              auVar91._0_8_ = auVar90._0_8_;
              auVar91._12_4_ = auVar79._12_4_;
              auVar91._8_4_ = auVar79._8_4_ + iVar82;
              auVar94._0_12_ = auVar91._0_12_;
              auVar94._12_4_ = auVar91._12_4_ + iVar83;
              auVar77 = vectorShiftLeftIntegerWord(auVar87,auVar76);
              auVar78 = vectorShiftLeftIntegerWord(auVar88,auVar76);
              auVar93 = vectorShiftLeftIntegerWord(auVar94,auVar76);
              auVar87 = vectorPermute(auVar95,auVar95,*(undefined1 (*) [16])(uVar46 & 0xfffffff0));
              auVar87 = vectorAddFloatingPoint(auVar95,auVar87);
              auVar84 = vectorMaximumFloatingPoint
                                  (auVar92 | auVar77 | auVar93,auVar84 | auVar89 | auVar78);
              auVar77 = vectorPermute(auVar84,auVar84,*(undefined1 (*) [16])(uVar46 & 0xfffffff0));
              auVar84 = vectorMaximumFloatingPoint(auVar84,auVar77);
              auVar84 = vectorPermute(auVar87,auVar84,*(undefined1 (*) [16])(uVar47 & 0xfffffff0));
              *pauVar54 = auVar84;
              lVar70 = lVar70 + -1;
              pauVar54 = pauVar54 + 1;
            } while (lVar70 != 0);
            pauVar67 = pauVar67 + 8;
            if (pauVar67 == &local_180) break;
            lVar62 = lVar62 + 0x200;
          }
          fVar6 = *(float *)(puVar51 + -0x7fcc);
          lVar62 = 0x40;
          pfVar57 = (float *)(iVar64 + 0x504);
          pauVar67 = (undefined1 (*) [16])&local_980;
          do {
            *pfVar57 = *(float *)*pauVar67 * fVar6;
            pfVar57[1] = *(float *)((int)*pauVar67 + 4);
            pfVar57[2] = *(float *)((int)*pauVar67 + 8) * fVar6;
            pfVar57[3] = *(float *)((int)*pauVar67 + 0xc);
            pfVar57[4] = *(float *)pauVar67[1] * fVar6;
            pfVar57[5] = *(float *)((int)pauVar67[1] + 4);
            pfVar57[6] = *(float *)((int)pauVar67[1] + 8) * fVar6;
            pfVar57[7] = *(float *)((int)pauVar67[1] + 0xc);
            lVar62 = lVar62 + -1;
            pfVar57 = pfVar57 + 8;
            pauVar67 = pauVar67 + 2;
          } while (lVar62 != 0);
        }
        pfVar57 = (float *)(iVar64 + 0x504);
        if (((*(uint *)(iVar64 + 0x394) & 0x40) != 0) &&
           (piVar49 = *(int **)(puVar51 + -0x7da8), *piVar49 == 0)) {
          fVar6 = *(float *)(puVar51 + -0x7fd4);
          lVar62 = 0x10;
          pfVar61 = pfVar57;
          do {
            fVar6 = fVar6 + *pfVar61 + pfVar61[2] + pfVar61[8] + pfVar61[0xc] + pfVar61[0x10] +
                    pfVar61[0x14] + pfVar61[0x18] + pfVar61[0x1c] +
                    pfVar61[4] + pfVar61[6] + pfVar61[10] + pfVar61[0xe] + pfVar61[0x12] +
                    pfVar61[0x16] + pfVar61[0x1a] + pfVar61[0x1e];
            lVar62 = lVar62 + -1;
            pfVar61 = pfVar61 + 0x20;
          } while (lVar62 != 0);
          fVar7 = *(float *)(puVar51 + -0x7d84);
          lVar62 = 0x10;
          iVar53 = 0;
          do {
            fVar3 = *(float *)((int)pfVar57 + iVar53 + 4);
            fVar9 = *(float *)((int)pfVar57 + iVar53 + 0x14);
            fVar12 = *(float *)((int)pfVar57 + iVar53 + 0x1c);
            fVar13 = *(float *)((int)pfVar57 + iVar53 + 0xc);
            fVar14 = *(float *)((int)pfVar57 + iVar53 + 0x34);
            fVar15 = *(float *)((int)pfVar57 + iVar53 + 0x3c);
            fVar16 = *(float *)((int)pfVar57 + iVar53 + 0x94);
            fVar59 = *(float *)((int)pfVar57 + iVar53 + 0x24);
            fVar17 = *(float *)((int)pfVar57 + iVar53 + 0x2c);
            fVar18 = *(float *)((int)pfVar57 + iVar53 + 0x44);
            fVar19 = *(float *)((int)pfVar57 + iVar53 + 0x54);
            if (fVar7 - fVar3 < 0.0) {
              fVar7 = fVar3;
            }
            fVar3 = *(float *)((int)pfVar57 + iVar53 + 0x74);
            fVar20 = *(float *)((int)pfVar57 + iVar53 + 0x5c);
            if (fVar9 - fVar12 < 0.0) {
              fVar9 = fVar12;
            }
            fVar12 = *(float *)((int)pfVar57 + iVar53 + 0x7c);
            fVar21 = *(float *)((int)pfVar57 + iVar53 + 0xbc);
            fVar22 = *(float *)((int)pfVar57 + iVar53 + 0x4c);
            if (fVar14 - fVar15 < 0.0) {
              fVar14 = fVar15;
            }
            fVar15 = *(float *)((int)pfVar57 + iVar53 + 0x9c);
            fVar23 = *(float *)((int)pfVar57 + iVar53 + 100);
            fVar24 = *(float *)((int)pfVar57 + iVar53 + 0x6c);
            fVar25 = *(float *)((int)pfVar57 + iVar53 + 0x84);
            fVar26 = *(float *)((int)pfVar57 + iVar53 + 0x8c);
            fVar27 = *(float *)((int)pfVar57 + iVar53 + 0xa4);
            fVar28 = *(float *)((int)pfVar57 + iVar53 + 0xac);
            fVar29 = *(float *)((int)pfVar57 + iVar53 + 0xc4);
            if (fVar3 - fVar12 < 0.0) {
              fVar3 = fVar12;
            }
            fVar12 = *(float *)((int)pfVar57 + iVar53 + 0xcc);
            if (fVar19 - fVar20 < 0.0) {
              fVar19 = fVar20;
            }
            fVar20 = *(float *)((int)pfVar57 + iVar53 + 0xd4);
            fVar30 = *(float *)((int)pfVar57 + iVar53 + 0xdc);
            if (fVar16 - fVar15 < 0.0) {
              fVar16 = fVar15;
            }
            fVar15 = *(float *)((int)pfVar57 + iVar53 + 0xfc);
            fVar31 = *(float *)((int)pfVar57 + iVar53 + 0xe4);
            fVar32 = *(float *)((int)pfVar57 + iVar53 + 0xec);
            if (fVar7 - fVar13 < 0.0) {
              fVar7 = fVar13;
            }
            fVar13 = *(float *)((int)pfVar57 + iVar53 + 0xb4);
            fVar33 = *(float *)((int)pfVar57 + iVar53 + 0xf4);
            if (fVar20 - fVar30 < 0.0) {
              fVar20 = fVar30;
            }
            if (fVar13 - fVar21 < 0.0) {
              fVar13 = fVar21;
            }
            if (fVar7 - fVar9 < 0.0) {
              fVar7 = fVar9;
            }
            if (fVar33 - fVar15 < 0.0) {
              fVar33 = fVar15;
            }
            if (fVar7 - fVar59 < 0.0) {
              fVar7 = fVar59;
            }
            if (fVar7 - fVar17 < 0.0) {
              fVar7 = fVar17;
            }
            if (fVar7 - fVar14 < 0.0) {
              fVar7 = fVar14;
            }
            if (fVar7 - fVar18 < 0.0) {
              fVar7 = fVar18;
            }
            if (fVar7 - fVar22 < 0.0) {
              fVar7 = fVar22;
            }
            if (fVar7 - fVar19 < 0.0) {
              fVar7 = fVar19;
            }
            if (fVar7 - fVar23 < 0.0) {
              fVar7 = fVar23;
            }
            if (fVar7 - fVar24 < 0.0) {
              fVar7 = fVar24;
            }
            if (fVar7 - fVar3 < 0.0) {
              fVar7 = fVar3;
            }
            if (fVar7 - fVar25 < 0.0) {
              fVar7 = fVar25;
            }
            if (fVar7 - fVar26 < 0.0) {
              fVar7 = fVar26;
            }
            if (fVar7 - fVar16 < 0.0) {
              fVar7 = fVar16;
            }
            if (fVar7 - fVar27 < 0.0) {
              fVar7 = fVar27;
            }
            if (fVar7 - fVar28 < 0.0) {
              fVar7 = fVar28;
            }
            if (fVar7 - fVar13 < 0.0) {
              fVar7 = fVar13;
            }
            if (fVar7 - fVar29 < 0.0) {
              fVar7 = fVar29;
            }
            if (fVar7 - fVar12 < 0.0) {
              fVar7 = fVar12;
            }
            if (fVar7 - fVar20 < 0.0) {
              fVar7 = fVar20;
            }
            if (fVar7 - fVar31 < 0.0) {
              fVar7 = fVar31;
            }
            if (fVar7 - fVar32 < 0.0) {
              fVar7 = fVar32;
            }
            if (fVar7 - fVar33 < 0.0) {
              fVar7 = fVar33;
            }
            lVar62 = lVar62 + -1;
            iVar53 = iVar53 + 0x40;
          } while (lVar62 != 0);
          fVar3 = *(float *)(puVar51 + -0x7ffc);
          fVar9 = *(float *)(puVar51 + -0x7d74);
          *(float *)(iVar64 + 0x420) = fVar7;
          fVar3 = (fVar6 * fVar9 + fVar3) * fVar3;
          *(float *)(iVar64 + 0x41c) = fVar3;
          if (((*(uint *)(iVar64 + 0x394) & 0x40) != 0) && (*piVar49 == 0)) {
            fVar6 = *(float *)(puVar51 + -0x7fe4);
            fVar9 = *(float *)(puVar51 + -0x7f2c);
            iVar82 = (*(uint *)(iVar64 + 0x428) >> 0x17 & 0xff) - 0x7f;
            fVar12 = *(float *)(puVar51 + -0x7f24);
            iVar53 = (*(uint *)(iVar64 + 0x424) >> 0x17 & 0xff) - 0x7f;
            fVar13 = *(float *)(puVar51 + -0x7f44);
            fVar14 = *(float *)(puVar51 + -0x7f3c);
            fVar15 = *(float *)(puVar51 + -0x7f1c);
            fVar16 = *(float *)(puVar51 + -0x7f34);
            fVar59 = *(float *)(puVar51 + -0x7f14);
            fVar20 = (float)(*(uint *)(iVar64 + 0x428) + iVar82 * -0x800000) - fVar6;
            fVar21 = (float)(*(uint *)(iVar64 + 0x424) + iVar53 * -0x800000) - fVar6;
            iVar83 = ((uint)fVar7 >> 0x17 & 0xff) - 0x7f;
            fVar17 = *(float *)(puVar51 + -0x7f0c);
            fVar18 = **(float **)(puVar51 + -0x7d7c);
            iVar81 = ((uint)fVar3 >> 0x17 & 0xff) - 0x7f;
            fVar19 = *(float *)(puVar51 + -0x7fd4);
            fVar20 = fVar20 * fVar20 * fVar20 * fVar20 *
                     (fVar20 * (fVar20 * (fVar20 * (fVar20 * fVar9 + fVar12) - fVar15) + fVar59) -
                     fVar17) + fVar20 * (fVar20 * (fVar20 * fVar13 - fVar14) + fVar16) +
                               (float)(longlong)iVar82;
            fVar21 = fVar21 * fVar21 * fVar21 * fVar21 *
                     (fVar21 * (fVar21 * (fVar21 * (fVar21 * fVar9 + fVar12) - fVar15) + fVar59) -
                     fVar17) + fVar21 * (fVar21 * (fVar21 * fVar13 - fVar14) + fVar16) +
                               (float)(longlong)iVar53;
            if (fVar18 != fVar19) {
              fVar6 = (float)((int)fVar7 + iVar83 * -0x800000) - fVar6;
              fVar6 = (fVar6 * fVar6 * fVar6 * fVar6 *
                       (fVar6 * (fVar6 * (fVar6 * (fVar6 * fVar9 + fVar12) - fVar15) + fVar59) -
                       fVar17) +
                      fVar6 * (fVar6 * (fVar6 * fVar13 - fVar14) + fVar16) + (float)(longlong)iVar83
                      ) - fVar21;
              if ((fVar19 < fVar6) || (fVar6 < fVar19)) {
                fVar21 = fVar6 * fVar18 + fVar21;
              }
              fVar6 = (float)((int)fVar3 + iVar81 * -0x800000) - *(float *)(puVar51 + -0x7fe4);
              fVar6 = (fVar6 * fVar6 * fVar6 * fVar6 *
                       (fVar6 * (fVar6 * (fVar6 * (fVar6 * fVar9 + fVar12) - fVar15) + fVar59) -
                       fVar17) +
                      fVar6 * (fVar6 * (fVar6 * fVar13 - fVar14) + fVar16) + (float)(longlong)iVar81
                      ) - fVar20;
              if (fVar6 <= *(float *)(puVar51 + -0x7fd4)) {
                if (fVar6 - *(float *)(puVar51 + -0x7fd4) < 0.0) {
                  fVar20 = fVar6 * fVar18 + fVar20;
                }
              }
              else {
                fVar20 = fVar6 * fVar18 + fVar20;
              }
            }
            fVar6 = *(float *)(puVar51 + -0x7fe4);
            fVar7 = fVar21 + fVar6;
            if (*(float *)(iVar64 + 0x434) < fVar7) {
              fVar21 = *(float *)(iVar64 + 0x434) - fVar6;
              fVar7 = fVar21 + fVar6;
            }
            fVar3 = *(float *)(puVar51 + -0x7f04);
            fVar9 = *(float *)(puVar51 + -0x7efc);
            fVar12 = *(float *)(puVar51 + -0x7ef4);
            fVar13 = *(float *)(puVar51 + -0x7eec);
            fVar14 = *(float *)(puVar51 + -0x7ee4);
            fVar15 = *(float *)(puVar51 + -0x7edc);
            fVar16 = *(float *)(puVar51 + -0x7ed4);
            if (fVar7 - *(float *)(iVar64 + 0x438) < 0.0) {
              fVar21 = *(float *)(iVar64 + 0x438) - fVar6;
            }
            iVar53 = (int)(fVar21 + (float)(~((int)fVar21 >> 0x1f) & 0x3f7fffff));
            fVar7 = ((float)(longlong)iVar53 - fVar21) * fVar3;
            fVar7 = fVar7 * fVar7 * fVar7 * fVar7 *
                    (fVar7 * (fVar7 * (fVar7 * fVar13 + fVar14) - fVar15) + fVar16) +
                    fVar7 * (fVar7 * (fVar7 * fVar9 + fVar12) - fVar6) + fVar6;
            fVar59 = fVar7 * (float)((iVar53 + 0x7f) * 0x800000);
            uVar52 = ((uint)fVar7 >> 0x17) + iVar53;
            if (0xff < uVar52) {
              fVar59 = (float)((int)~uVar52 >> 0x1f & 0x7f800000);
            }
            *(float *)(iVar64 + 0x424) = fVar59;
            iVar53 = (int)(fVar20 + (float)(~((int)fVar20 >> 0x1f) & 0x3f7fffff));
            local_170 = (ulonglong)iVar53;
            fVar3 = ((float)(longlong)local_170 - fVar20) * fVar3;
            fVar6 = fVar3 * fVar3 * fVar3 * fVar3 *
                    (fVar3 * (fVar3 * (fVar3 * fVar13 + fVar14) - fVar15) + fVar16) +
                    fVar3 * (fVar3 * (fVar3 * fVar9 + fVar12) - fVar6) + fVar6;
            local_168 = fVar6 * (float)((iVar53 + 0x7f) * 0x800000);
            uVar52 = ((uint)fVar6 >> 0x17) + iVar53;
            if (uVar52 < 0x100) {
              *(float *)(iVar64 + 0x428) = local_168;
            }
            else {
              *(uint *)(iVar64 + 0x428) = (int)~uVar52 >> 0x1f & 0x7f800000;
            }
          }
        }
      }
      **(undefined4 **)(iVar37 + 8) = 0x40a6c;
      *(undefined4 *)(*(int *)(iVar37 + 8) + 4) = 0x206;
      puVar60 = (undefined4 *)(*(int *)(iVar37 + 8) + 8);
      *(undefined4 **)(iVar37 + 8) = puVar60;
      *puVar60 = 0x40a70;
      *(undefined4 *)(*(int *)(iVar37 + 8) + 4) = 1;
      puVar60 = (undefined4 *)(*(int *)(iVar37 + 8) + 8);
      *(undefined4 **)(iVar37 + 8) = puVar60;
      *puVar60 = 0x40a74;
      *(undefined4 *)(*(int *)(iVar37 + 8) + 4) = 1;
      uVar55 = *(ulonglong *)(iVar34 + 0x40);
      *(int *)(iVar37 + 8) = *(int *)(iVar37 + 8) + 8;
      *(ulonglong *)(iVar34 + 0x40) = uVar55 | 0xc000000;
      _opd_FUN_00104960(0);
      iVar81 = iVar64 + 0xc0;
      _opd_FUN_0010a450(iVar81);
      _opd_FUN_0010a048(iVar64 + 0x40);
      _opd_FUN_000f4a10(0,iVar64 + 0x30);
      _opd_FUN_000f4a10(0x2e,local_990);
      iVar53 = *(int *)(puVar51 + -0x7d78);
      _opd_FUN_000f4a10(0x2a,iVar53);
      _opd_FUN_000f4a10(0x2b,iVar53 + 0x10);
      _opd_FUN_000f4a10(0x2c,iVar53 + 0x20);
      _opd_FUN_000f4a10(0x2d,iVar53 + 0x30);
      _opd_FUN_00154228(iVar64,iVar63);
      if ((uVar40 & 0x3037) != 0) {
        _opd_FUN_0014c6d0(iVar64,iVar63);
      }
      bVar1 = (uVar40 & 0x3036) != 0;
      _opd_FUN_0015c878();
      _opd_FUN_0015fe08(iVar81);
      _opd_FUN_0015f978(iVar64 + 0x40);
      if (bVar1) {
        if (((*(uint *)(iVar64 + 0x394) & 0x800) == 0) &&
           ((*(uint *)(iVar64 + 0x394) & 0x50000) == 0)) {
          _opd_FUN_0013d4f0(iVar64,iVar63);
        }
        else {
          _opd_FUN_0013f480(iVar64,iVar63);
        }
      }
      if (((*(uint *)(iVar64 + 0x394) & 0x1000) != 0) && ((*(uint *)(iVar64 + 0x394) & 0x2000) == 0)
         ) {
        _opd_FUN_0012dd38(iVar64,iVar63);
      }
      uVar52 = *(uint *)(iVar64 + 0x394);
      if ((uVar52 & 0x10) == 0) {
        _opd_FUN_00106bd0((double)*(float *)(puVar51 + -0x7fd4),uVar52 & 0xe);
      }
      else {
        _opd_FUN_00106bd0((double)*(float *)(puVar51 + -0x7fd4),uVar52 & 0xe);
      }
      if ((uVar40 & 0x10000) != 0) {
        _opd_FUN_00134948(iVar64,iVar63,0);
      }
      if ((uVar40 & 6) != 0) {
        auVar84 = (undefined1  [16])0x0;
        dVar74 = (double)*(float *)(iVar64 + 0x370);
        dVar73 = (double)*(float *)(iVar64 + 0x374);
        *(undefined4 *)(iVar64 + 0x370) = *(undefined4 *)(iVar64 + 0x378);
        *(undefined4 *)(iVar64 + 0x374) = *(undefined4 *)(iVar64 + 0x37c);
        _opd_FUN_000f56f0((double)*(float *)(iVar64 + 0x380),(double)*(float *)(iVar64 + 0x388),
                          (double)*(float *)(iVar64 + 0x38c),(double)*(float *)(iVar64 + 900),iVar64
                          ,iVar64);
        fVar7 = *(float *)(iVar64 + 0x408);
        dVar72 = (double)(*(float *)(iVar64 + 0x40c) - fVar7);
        uVar48 = *(undefined4 *)(iVar64 + 0x414);
        dVar75 = (double)*(float *)(puVar51 + -0x7fe4);
        uVar8 = *(undefined4 *)(iVar64 + 0x410);
        dVar71 = (double)(float)(1.0 / dVar72);
        dVar71 = (double)(float)(dVar71 * -(double)(float)(dVar72 * dVar71 - dVar75) + dVar71);
        fVar6 = (float)(dVar71 * -(double)(float)(dVar72 * dVar71 - dVar75) + dVar71);
        **(undefined4 **)(iVar37 + 8) = 0x141efc;
        *(undefined4 *)(*(int *)(iVar37 + 8) + 4) = 0x17;
        iVar53 = *(int *)(iVar37 + 8);
        *(float **)(iVar37 + 8) = (float *)(iVar53 + 8);
        *(float *)(iVar53 + 8) = local_a10;
        *(float *)(iVar53 + 0xc) = local_a0c;
        *(undefined4 *)(iVar53 + 0x10) = local_a08;
        *(undefined4 *)(iVar53 + 0x14) = local_a04;
        *(int *)(iVar37 + 8) = *(int *)(iVar37 + 8) + 0x10;
        _opd_FUN_0014f290(0x15,&local_a10);
        auVar89 = *(undefined1 (*) [16])(uVar38 & 0xfffffff0);
        *(uint *)(iVar64 + 0x3f0) = *(uint *)(iVar64 + 0x418);
        auVar77 = *(undefined1 (*) [16])(uVar39 & 0xfffffff0);
        auVar80._0_4_ = *(uint *)(iVar64 + 0x418) | 0xff;
        auVar80._4_12_ = auStack_99c;
        auVar76 = vectorPermute(auVar80,auVar97,auVar89);
        auVar76 = vectorConvertFromUnsignedFixedPointWord(auVar76,0);
        auStack_9b0 = vectorMultiplyAddFloatingPoint(auVar76,auVar77,auVar97);
        _opd_FUN_0014f290(0x16,auStack_9b0);
        _opd_FUN_0010a450(iVar81);
        _opd_FUN_0015fe08(iVar81);
        if ((uVar40 & 2) != 0) {
          _opd_FUN_0011a230(iVar64,iVar63);
        }
        _opd_FUN_00106bd0((double)*(float *)(puVar51 + -0x7fd4),0xc);
        *(float *)(iVar64 + 0x370) = (float)dVar74;
        *(float *)(iVar64 + 0x374) = (float)dVar73;
        _opd_FUN_000f56f0((double)*(float *)(iVar64 + 0x380),(double)*(float *)(iVar64 + 0x388),
                          (double)*(float *)(iVar64 + 0x38c),(double)*(float *)(iVar64 + 900),iVar64
                          ,iVar64);
        fVar9 = *(float *)(iVar64 + 0x3f4);
        dVar72 = (double)(*(float *)(iVar64 + 0x3f8) - fVar9);
        uVar10 = *(undefined4 *)(iVar64 + 0x400);
        uVar11 = *(undefined4 *)(iVar64 + 0x3fc);
        dVar71 = (double)(float)(1.0 / dVar72);
        dVar71 = (double)(float)(dVar71 * -(double)(float)(dVar72 * dVar71 - dVar75) + dVar71);
        fVar3 = (float)(dVar71 * -(double)(float)(dVar72 * dVar71 - dVar75) + dVar71);
        **(undefined4 **)(iVar37 + 8) = 0x141efc;
        *(undefined4 *)(*(int *)(iVar37 + 8) + 4) = 0x17;
        iVar53 = *(int *)(iVar37 + 8);
        *(float **)(iVar37 + 8) = (float *)(iVar53 + 8);
        *(float *)(iVar53 + 8) = local_980;
        *(float *)(iVar53 + 0xc) = local_97c;
        *(undefined4 *)(iVar53 + 0x10) = local_978;
        *(undefined4 *)(iVar53 + 0x14) = local_974;
        *(int *)(iVar37 + 8) = *(int *)(iVar37 + 8) + 0x10;
        _opd_FUN_0014f290(0x15,&local_980);
        *(uint *)(iVar64 + 0x3f0) = *(uint *)(iVar64 + 0x404);
        auVar97._0_4_ = *(uint *)(iVar64 + 0x404) | 0xff;
        auVar97._4_12_ = auStack_9cc;
        auVar97 = vectorPermute(auVar97,auVar84,auVar89);
        auVar97 = vectorConvertFromUnsignedFixedPointWord(auVar97,0);
        auStack_9c0 = vectorMultiplyAddFloatingPoint(auVar97,auVar77,auVar84);
        _opd_FUN_0014f290(0x16,auStack_9c0);
        _opd_FUN_0010a450(iVar81);
        _opd_FUN_0015fe08(iVar81);
        local_a08 = uVar48;
        local_a04 = uVar8;
        local_a10 = fVar6;
        local_978 = uVar10;
        local_974 = uVar11;
        local_980 = fVar3;
        local_a0c = -fVar7 * fVar6;
        local_97c = -fVar9 * fVar3;
      }
      if (bVar1) {
        _opd_FUN_0013ac78(iVar64,iVar63);
      }
      if ((uVar40 & 0x10) != 0) {
        _opd_FUN_0011a4c8(iVar64,iVar63);
      }
      if ((uVar40 & 0x20) != 0) {
        _opd_FUN_001150e0(iVar64,iVar63);
      }
      bVar1 = (uVar40 & 0x40) != 0;
      if (bVar1) {
        _opd_FUN_00149a80(iVar64,iVar63);
      }
      if (((*(uint *)(iVar64 + 0x394) & 0x1000) != 0) && ((*(uint *)(iVar64 + 0x394) & 0x2000) == 0)
         ) {
        _opd_FUN_0012ca70(iVar64,iVar63);
      }
      _opd_FUN_0012d598(iVar64,iVar63,0);
      if ((uVar40 & 0x1000) != 0) {
        _opd_FUN_0011a368(iVar64,iVar63);
      }
      if ((uVar40 & 0x2000) == 0) {
        if (!bVar1) goto LAB_000f9ec4;
LAB_000fa0e8:
        _opd_FUN_001499d0(iVar64,iVar63);
        _opd_FUN_0012cf58(iVar64,iVar63);
        if ((uVar40 & 0x800) != 0) goto LAB_000fa114;
LAB_000f9ee0:
        if ((uVar40 & 0x3076) == 0) goto LAB_000f9ef0;
LAB_000fa134:
        _opd_FUN_0013a768(iVar64,iVar63);
        if ((uVar40 & 0x80) != 0) goto LAB_000fa150;
LAB_000f9efc:
        if ((uVar40 & 0x100) == 0) goto LAB_000f9f08;
LAB_000fa16c:
        _opd_FUN_00136ec8(iVar64,iVar63);
        if ((uVar40 & 0x20000) != 0) goto LAB_000fa188;
LAB_000f9f14:
        if ((uVar40 & 0x40000) == 0) goto LAB_000f9f20;
LAB_000fa1a8:
        _opd_FUN_00134948(iVar64,iVar63,2);
        _opd_FUN_000f6600(iVar64,iVar63);
        *(byte *)(iVar34 + 0x40) = *(byte *)(iVar34 + 0x40) & 0xfe;
        *(ulonglong *)(iVar34 + 0x40) = *(ulonglong *)(iVar34 + 0x40) & 0xff0fffffffffffff;
        if ((uVar40 & 0x80000) != 0) goto LAB_000fa1f0;
LAB_000f9f54:
        uVar38 = *(uint *)(iVar64 + 0x394);
      }
      else {
        _opd_FUN_00114f80(iVar64,iVar63);
        if (bVar1) goto LAB_000fa0e8;
LAB_000f9ec4:
        _opd_FUN_0012cf58(iVar64,iVar63);
        if ((uVar40 & 0x800) == 0) goto LAB_000f9ee0;
LAB_000fa114:
        _opd_FUN_0014bf00(iVar64,iVar63);
        if ((uVar40 & 0x3076) != 0) goto LAB_000fa134;
LAB_000f9ef0:
        if ((uVar40 & 0x80) == 0) goto LAB_000f9efc;
LAB_000fa150:
        _opd_FUN_001541e0(iVar64,iVar63);
        if ((uVar40 & 0x100) != 0) goto LAB_000fa16c;
LAB_000f9f08:
        if ((uVar40 & 0x20000) == 0) goto LAB_000f9f14;
LAB_000fa188:
        _opd_FUN_00134948(iVar64,iVar63,1);
        if ((uVar40 & 0x40000) != 0) goto LAB_000fa1a8;
LAB_000f9f20:
        _opd_FUN_000f6600(iVar64,iVar63);
        *(byte *)(iVar34 + 0x40) = *(byte *)(iVar34 + 0x40) & 0xfe;
        *(ulonglong *)(iVar34 + 0x40) = *(ulonglong *)(iVar34 + 0x40) & 0xff0fffffffffffff;
        if ((uVar40 & 0x80000) == 0) goto LAB_000f9f54;
LAB_000fa1f0:
        _opd_FUN_00134948(iVar64,iVar63,3);
        uVar38 = *(uint *)(iVar64 + 0x394);
      }
      if ((uVar38 & 0x1000000) != 0) {
        _opd_FUN_00104ae8(iVar64 + 0x4a8,*(undefined4 *)(iVar64 + 0x494),
                          *(undefined4 *)(iVar64 + 0x498),*(undefined4 *)(iVar64 + 0x49c),
                          *(undefined4 *)(iVar64 + 0x4a0),0);
      }
      _opd_FUN_0015efb0();
      _opd_FUN_0015c6f0();
      uVar48 = *(undefined4 *)(iVar34 + 0x248);
      _opd_FUN_000e73e8();
      uVar58 = *(ulonglong *)(iVar34 + 0x40) >> 0x21 & 0x7ff;
      **(undefined4 **)(iVar37 + 8) = 0x41800;
      *(uint *)(*(int *)(iVar37 + 8) + 4) = (uint)(uVar58 << 4) | 0x1000000;
      iVar53 = *(int *)(iVar34 + 0x248);
      uVar55 = *(ulonglong *)(iVar34 + 0x40);
      *(int *)(iVar37 + 8) = *(int *)(iVar37 + 8) + 8;
      *(int *)(iVar34 + 0x248) = iVar53 + 1;
      *(ulonglong *)(iVar34 + 0x40) =
           ((uVar55 >> 0x21 & 0x7ff) + 1 & 0x7ff) << 0x21 | uVar55 & 0xfffff001ffffffff;
      *(short *)(iVar53 * 2 + iVar34 + 0x24c) = (short)uVar58;
      piVar49 = (int *)(iVar64 + 0x3a4);
      *(undefined4 *)(iVar64 + 0x152c) = uVar48;
      *(uint *)(iVar64 + 0x394) = uVar35 & 0xffffdfff;
      iVar64 = *piVar49;
      local_9e0 = fVar2;
      local_9dc = -fVar4 * fVar2;
      local_9d8 = uVar36;
      local_9d4 = uVar5;
      if (*piVar49 == 0) goto LAB_000fa00c;
    }
    iVar34 = *(int *)(iVar64 + 0x3a4);
    *(uint *)(iVar64 + 0x394) = uVar35 & 0xffffdfff;
  }
LAB_000fa00c:
  _opd_FUN_0012ffc8();
  return;
}

