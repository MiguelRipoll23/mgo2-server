// function FUN_0001a800 @ 0001a800 (target 1a800)

void FUN_0001a800(longlong param_1)

{
  char cVar1;
  char cVar2;
  int iVar3;
  int iVar4;
  longlong lVar5;
  ulonglong uVar6;
  undefined8 uVar7;
  ulonglong unaff_r23;
  int *piVar8;
  int *piVar9;
  int iVar10;
  longlong lVar11;
  longlong lVar12;
  longlong lVar13;
  longlong *plVar15;
  longlong lVar14;
  byte in_cr0;
  byte in_cr1;
  byte in_cr2;
  byte in_cr3;
  byte unaff_cr4;
  byte in_cr5;
  byte in_cr6;
  byte in_cr7;
  uint uStack00000008;
  undefined1 auStack_600 [1024];
  undefined1 auStack_200 [128];
  undefined1 auStack_180 [20];
  int iStack_16c;
  int iStack_154;
  undefined1 local_118 [104];
  undefined1 auStack_b0 [56];
  
  uStack00000008 =
       (uint)(in_cr0 & 0xf) << 0x1c | (uint)(in_cr1 & 0xf) << 0x18 | (uint)(in_cr2 & 0xf) << 0x14 |
       (uint)(in_cr3 & 0xf) << 0x10 | (uint)(unaff_cr4 & 0xf) << 0xc | (uint)(in_cr5 & 0xf) << 8 |
       (uint)(in_cr6 & 0xf) << 4 | (uint)(in_cr7 & 0xf);
  *(undefined8 *)uRam001ec370 = uRam001ec498;
  FUN_0001bdf8(0);
  uVar7 = uRam001ec478;
  piVar8 = (int *)uRam001ec4a0;
  *piVar8 = 0;
  piVar9 = (int *)uVar7;
  if (0 < *piVar9) {
    lVar11 = 0;
    lVar14 = lRam001ec480;
    do {
      plVar15 = (longlong *)lVar14;
      lVar13 = *plVar15;
      iVar10 = (int)lVar11 + 1;
      lVar12 = plVar15[1];
      lVar11 = (longlong)iVar10;
      if (lVar13 != 0) {
        iVar3 = *(int *)(plVar15 + 2);
        iVar4 = *(int *)((int)plVar15 + 0x14);
        cVar1 = *(char *)(plVar15 + 3);
        cVar2 = *(char *)((int)plVar15 + 0x19);
        FUN_001a7ee8(auStack_600,uRam001ec488,lVar13);
        FUN_001a7ee8(auStack_200,uRam001ec4a8,lVar11,*piVar9,lVar13);
        FUN_0001c0d0((double)(float)((double)lVar11 / (double)(longlong)*piVar9),uRam001ec4b0,
                     auStack_200);
        lVar5 = FUN_001aee08(auStack_600,auStack_180);
        if (lVar5 != 0) {
                    /* WARNING: Subroutine does not return */
          FUN_00013c58(uRam001ec4c8,lVar13);
        }
        if ((param_1 == 0) && (cVar1 == '\0')) {
          if (iVar3 != iStack_154) {
LAB_0001a97c:
                    /* WARNING: Subroutine does not return */
            FUN_00013c58(uRam001ec4b8,iStack_154,iVar3);
          }
          if (iStack_16c != iVar4) {
                    /* WARNING: Subroutine does not return */
            FUN_00013c58(uRam001ec4c0,iStack_16c,iVar4);
          }
        }
        else {
          if (iVar3 != iStack_154) goto LAB_0001a97c;
          if (iStack_16c != iVar4) {
                    /* WARNING: Subroutine does not return */
            FUN_00013c58(uRam001ec4c0,iStack_16c,iVar4);
          }
          FUN_00019ac0(auStack_600,auStack_b0);
          if ((cVar2 == '\0') && (lVar5 = FUN_001a9298(auStack_b0,lVar12), lVar5 != 0)) {
                    /* WARNING: Subroutine does not return */
            FUN_00013c58(uRam001ec528,lVar5,lVar13,lVar12,auStack_b0);
          }
          FUN_0019e950(auStack_600,local_118);
        }
      }
      lVar14 = lVar14 + 0x20;
    } while (iVar10 < *piVar9);
    if (*piVar8 != 0) {
      FUN_00011f18();
      FUN_0001bdf8(1);
      if (unaff_r23 < 0x271000001) {
        if (unaff_r23 < 0x9c4001) {
          if (unaff_r23 < 0x2711) {
            uVar6 = unaff_r23 & 0xffffffff;
            uVar7 = uRam001ec438;
          }
          else {
            uVar6 = unaff_r23 >> 10 & 0xffffffff;
            uVar7 = uRam001ec430;
          }
        }
        else {
          uVar6 = unaff_r23 >> 0x14 & 0xffffffff;
          uVar7 = uRam001ec420;
        }
      }
      else {
        uVar6 = unaff_r23 >> 0x1e & 0xffffffff;
        uVar7 = uRam001ec428;
      }
                    /* WARNING: Subroutine does not return */
      FUN_00013c58(uRam001ec4e8,*piVar8,uVar6,uVar7);
    }
  }
  *(undefined1 *)uRam001ec3b0 = 1;
  FUN_0001c2c0();
  *(undefined4 *)uRam001ec508 = 3;
  return;
}

