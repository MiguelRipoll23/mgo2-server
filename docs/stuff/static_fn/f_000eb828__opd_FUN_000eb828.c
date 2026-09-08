/* containing function of static patch target(s); entry .opd.FUN_000eb828 @ 000eb828 */

void _opd_FUN_000eb828(void)

{
  short sVar1;
  int iVar2;
  int iVar3;
  undefined *puVar4;
  int iVar5;
  int iVar6;
  undefined1 *puVar7;
  int *piVar8;
  code *pcVar9;
  longlong lVar10;
  
  puVar4 = PTR_PTR_0121acec;
  iVar2 = *(int *)(PTR_PTR_0121acec + -0x8000);
  lVar10 = 0x20;
  *(undefined **)(iVar2 + 100) = &DAT_01900000;
  *(undefined2 *)(iVar2 + 0x56) = 0x20;
  piVar8 = (int *)(iVar2 + 0x10294);
  *(code **)(iVar2 + 0x3c) = _opd_FUN_00010278 + iVar2;
  *(undefined2 *)(iVar2 + 0x44) = 0;
  *(undefined2 *)(iVar2 + 0x46) = 0x1f;
  *(undefined2 *)(iVar2 + 0x52) = 0xffff;
  *(int *)(iVar2 + 0x74) = iVar2 + 0x10680;
  *(undefined4 *)(iVar2 + 0x60) = 0;
  *(undefined2 *)(iVar2 + 0x5e) = 0;
  *(undefined2 *)(iVar2 + 0x54) = 0;
  *(undefined2 *)(iVar2 + 0x58) = 0;
  *(undefined2 *)(iVar2 + 0x5a) = 0;
  *(undefined2 *)(iVar2 + 0x5c) = 0;
  *(undefined2 *)(iVar2 + 0x40) = 0xffff;
  *(undefined2 *)(iVar2 + 0x42) = 0xffff;
  *(undefined2 *)(iVar2 + 0x48) = 0xffff;
  *(undefined2 *)(iVar2 + 0x4a) = 0xffff;
  *(undefined2 *)(iVar2 + 0x4c) = 0xffff;
  *(undefined2 *)(iVar2 + 0x4e) = 0xffff;
  *(undefined2 *)(iVar2 + 0x50) = 0xffff;
  iVar5 = 0;
  puVar7 = (undefined1 *)(iVar2 + 0x10280);
  do {
    iVar6 = iVar5 + 1;
    puVar7[2] = 0;
    *(short *)(piVar8 + -3) = (short)iVar5 + -1;
    *piVar8 = iVar6;
    *(short *)((int)piVar8 + -10) = (short)iVar6;
    *(undefined2 *)(piVar8 + -2) = 0;
    *puVar7 = 0;
    puVar7[1] = 0;
    piVar8 = piVar8 + 8;
    lVar10 = lVar10 + -1;
    iVar5 = iVar6;
    puVar7 = puVar7 + 0x20;
  } while (lVar10 != 0);
  sVar1 = *(short *)(iVar2 + 0x5e);
  *(undefined2 *)(_opd_FUN_00010668 + iVar2 + 2) = 0xffff;
  *(undefined2 *)(iVar2 + 0x10288) = 0xffff;
  if (sVar1 == 0) {
    iVar5 = *(int *)(iVar2 + 0x1c);
    *(undefined **)(iVar2 + 0x1c) = &DAT_01900000 + iVar5;
    *(int *)(iVar2 + 0x24) = *(int *)(iVar2 + 0x24) + -0x1900000;
    *(int *)(iVar2 + 0x68) = iVar5 + *(int *)(iVar2 + 0x20);
    *(undefined4 *)(iVar2 + 0x70) = 0;
  }
  else {
    iVar5 = *(int *)(iVar2 + 0x28);
    *(undefined **)(iVar2 + 0x28) = &DAT_01900000 + iVar5;
    *(int *)(iVar2 + 0x30) = *(int *)(iVar2 + 0x30) + -0x1900000;
    *(int *)(iVar2 + 0x68) = iVar5 + *(int *)(iVar2 + 0x2c);
    *(undefined4 *)(iVar2 + 0x70) = 1;
  }
  FUN_00fba95c(*(undefined4 *)(iVar2 + 0x68),iVar2 + 0x6c);
  lVar10 = 0x1000;
  *(undefined4 *)((int)&Elf64_Phdr_ARRAY_00010040[5].p_offset + iVar2 + 4) = 0x600000;
  *(undefined2 *)((int)&Elf64_Phdr_ARRAY_00010040[4].p_align + iVar2 + 6) = 0x1000;
  piVar8 = (int *)(iVar2 + 0x1071c);
  *(undefined2 *)((int)&Elf64_Phdr_ARRAY_00010040[5].p_flags + iVar2 + 2) = 1;
  *(int *)((int)&Elf64_Phdr_ARRAY_00010040[4].p_paddr + iVar2 + 4) = iVar2 + 0x10700;
  *(undefined2 *)((int)&Elf64_Phdr_ARRAY_00010040[4].p_filesz + iVar2 + 4) = 0;
  *(undefined2 *)((int)&Elf64_Phdr_ARRAY_00010040[4].p_filesz + iVar2 + 6) = 0xfff;
  *(undefined2 *)((int)&Elf64_Phdr_ARRAY_00010040[4].p_align + iVar2 + 2) = 0xffff;
  *(int *)((int)&Elf64_Phdr_ARRAY_00010040[5].p_paddr + iVar2 + 4) = iVar2 + 0x30700;
  *(undefined4 *)((int)&Elf64_Phdr_ARRAY_00010040[5].p_offset + iVar2) = 0;
  *(undefined2 *)((int)&Elf64_Phdr_ARRAY_00010040[4].p_align + iVar2 + 4) = 0;
  *(undefined2 *)((int)&Elf64_Phdr_ARRAY_00010040[5].p_type + iVar2) = 0;
  *(undefined2 *)((int)&Elf64_Phdr_ARRAY_00010040[5].p_type + iVar2 + 2) = 0;
  *(undefined2 *)((int)&Elf64_Phdr_ARRAY_00010040[5].p_flags + iVar2) = 0;
  *(undefined2 *)((int)&Elf64_Phdr_ARRAY_00010040[4].p_filesz + iVar2) = 0xffff;
  *(undefined2 *)((int)&Elf64_Phdr_ARRAY_00010040[4].p_filesz + iVar2 + 2) = 0xffff;
  *(undefined2 *)((int)&Elf64_Phdr_ARRAY_00010040[4].p_memsz + iVar2) = 0xffff;
  *(undefined2 *)((int)&Elf64_Phdr_ARRAY_00010040[4].p_memsz + iVar2 + 2) = 0xffff;
  *(undefined2 *)((int)&Elf64_Phdr_ARRAY_00010040[4].p_memsz + iVar2 + 4) = 0xffff;
  *(undefined2 *)((int)&Elf64_Phdr_ARRAY_00010040[4].p_memsz + iVar2 + 6) = 0xffff;
  *(undefined2 *)((int)&Elf64_Phdr_ARRAY_00010040[4].p_align + iVar2) = 0xffff;
  iVar5 = 0;
  puVar7 = (undefined1 *)(iVar2 + 0x10708);
  do {
    iVar6 = iVar5 + 1;
    puVar7[2] = 0;
    *(short *)(piVar8 + -3) = (short)iVar5 + -1;
    *piVar8 = iVar6;
    *(short *)((int)piVar8 + -10) = (short)iVar6;
    *(undefined2 *)(piVar8 + -2) = 0;
    *puVar7 = 1;
    puVar7[1] = 2;
    piVar8 = piVar8 + 8;
    lVar10 = lVar10 + -1;
    iVar5 = iVar6;
    puVar7 = puVar7 + 0x20;
  } while (lVar10 != 0);
  sVar1 = *(short *)((int)&Elf64_Phdr_ARRAY_00010040[5].p_flags + iVar2 + 2);
  *(undefined2 *)(iVar2 + 0x10710) = 0xffff;
  *(undefined2 *)(iVar2 + 0x306f2) = 0xffff;
  if (sVar1 == 0) {
    iVar5 = *(int *)(iVar2 + 0x1c);
    iVar6 = *(int *)(iVar2 + 0x24);
    iVar3 = *(int *)(iVar2 + 0x20);
    *(undefined4 *)((int)&Elf64_Phdr_ARRAY_00010040[5].p_paddr + iVar2) = 0;
    *(int *)(iVar2 + 0x1c) = iVar5 + 0x600000;
    *(int *)(iVar2 + 0x24) = iVar6 + -0x600000;
    *(int *)((int)&Elf64_Phdr_ARRAY_00010040[5].p_vaddr + iVar2) = iVar5 + iVar3;
  }
  else {
    iVar5 = *(int *)(iVar2 + 0x28);
    iVar6 = *(int *)(iVar2 + 0x30);
    iVar3 = *(int *)(iVar2 + 0x2c);
    *(int *)(iVar2 + 0x28) = iVar5 + 0x600000;
    *(undefined4 *)((int)&Elf64_Phdr_ARRAY_00010040[5].p_paddr + iVar2) = 1;
    *(int *)(iVar2 + 0x30) = iVar6 + -0x600000;
    *(int *)((int)&Elf64_Phdr_ARRAY_00010040[5].p_vaddr + iVar2) = iVar5 + iVar3;
  }
  FUN_00fba95c(*(undefined4 *)((int)&Elf64_Phdr_ARRAY_00010040[5].p_vaddr + iVar2),iVar2 + 0x1016c);
  lVar10 = 0x800;
  *(undefined4 *)((int)&Elf64_Phdr_ARRAY_00010040[4].p_offset + iVar2) = 0x100000;
  piVar8 = (int *)(iVar2 + 0x3471c);
  *(undefined2 *)((int)&Elf64_Phdr_ARRAY_00010040[3].p_align + iVar2 + 2) = 0x800;
  *(int *)((int)&Elf64_Phdr_ARRAY_00010040[4].p_paddr + iVar2) = iVar2 + 0x44700;
  *(undefined2 *)((int)&Elf64_Phdr_ARRAY_00010040[4].p_type + iVar2 + 2) = 1;
  *(int *)((int)&Elf64_Phdr_ARRAY_00010040[3].p_paddr + iVar2) = iVar2 + 0x34700;
  *(undefined2 *)((int)&Elf64_Phdr_ARRAY_00010040[3].p_filesz + iVar2) = 0;
  *(undefined2 *)((int)&Elf64_Phdr_ARRAY_00010040[3].p_filesz + iVar2 + 2) = 0x7ff;
  *(undefined2 *)((int)&Elf64_Phdr_ARRAY_00010040[3].p_memsz + iVar2 + 6) = 0xffff;
  *(undefined4 *)((int)&Elf64_Phdr_ARRAY_00010040[4].p_flags + iVar2) = 0;
  *(undefined2 *)((int)&Elf64_Phdr_ARRAY_00010040[3].p_align + iVar2) = 0;
  *(undefined2 *)((int)&Elf64_Phdr_ARRAY_00010040[3].p_align + iVar2 + 4) = 0;
  *(undefined2 *)((int)&Elf64_Phdr_ARRAY_00010040[3].p_align + iVar2 + 6) = 0;
  *(undefined2 *)((int)&Elf64_Phdr_ARRAY_00010040[4].p_type + iVar2) = 0;
  *(undefined2 *)((int)&Elf64_Phdr_ARRAY_00010040[3].p_paddr + iVar2 + 4) = 0xffff;
  *(undefined2 *)((int)&Elf64_Phdr_ARRAY_00010040[3].p_paddr + iVar2 + 6) = 0xffff;
  *(undefined2 *)((int)&Elf64_Phdr_ARRAY_00010040[3].p_filesz + iVar2 + 4) = 0xffff;
  *(undefined2 *)((int)&Elf64_Phdr_ARRAY_00010040[3].p_filesz + iVar2 + 6) = 0xffff;
  *(undefined2 *)((int)&Elf64_Phdr_ARRAY_00010040[3].p_memsz + iVar2) = 0xffff;
  *(undefined2 *)((int)&Elf64_Phdr_ARRAY_00010040[3].p_memsz + iVar2 + 2) = 0xffff;
  *(undefined2 *)((int)&Elf64_Phdr_ARRAY_00010040[3].p_memsz + iVar2 + 4) = 0xffff;
  iVar5 = 0;
  puVar7 = (undefined1 *)(iVar2 + 0x34708);
  do {
    iVar6 = iVar5 + 1;
    puVar7[2] = 0;
    *(short *)(piVar8 + -3) = (short)iVar5 + -1;
    *piVar8 = iVar6;
    *(short *)((int)piVar8 + -10) = (short)iVar6;
    *(undefined2 *)(piVar8 + -2) = 0;
    *puVar7 = 1;
    puVar7[1] = 3;
    piVar8 = piVar8 + 8;
    lVar10 = lVar10 + -1;
    iVar5 = iVar6;
    puVar7 = puVar7 + 0x20;
  } while (lVar10 != 0);
  sVar1 = *(short *)((int)&Elf64_Phdr_ARRAY_00010040[4].p_type + iVar2 + 2);
  *(undefined2 *)(iVar2 + 0x34710) = 0xffff;
  *(undefined2 *)(iVar2 + 0x446f2) = 0xffff;
  if (sVar1 == 0) {
    iVar5 = *(int *)(iVar2 + 0x1c);
    iVar6 = *(int *)(iVar2 + 0x24);
    iVar3 = *(int *)(iVar2 + 0x20);
    *(undefined4 *)((int)&Elf64_Phdr_ARRAY_00010040[4].p_vaddr + iVar2 + 4) = 0;
    *(int *)(iVar2 + 0x1c) = iVar5 + 0x100000;
    *(int *)(iVar2 + 0x24) = iVar6 + -0x100000;
    *(int *)((int)&Elf64_Phdr_ARRAY_00010040[4].p_offset + iVar2 + 4) = iVar5 + iVar3;
  }
  else {
    iVar5 = *(int *)(iVar2 + 0x28);
    iVar6 = *(int *)(iVar2 + 0x30);
    iVar3 = *(int *)(iVar2 + 0x2c);
    *(int *)(iVar2 + 0x28) = iVar5 + 0x100000;
    *(undefined4 *)((int)&Elf64_Phdr_ARRAY_00010040[4].p_vaddr + iVar2 + 4) = 1;
    *(int *)(iVar2 + 0x30) = iVar6 + -0x100000;
    *(int *)((int)&Elf64_Phdr_ARRAY_00010040[4].p_offset + iVar2 + 4) = iVar5 + iVar3;
  }
  FUN_00fba95c(*(undefined4 *)((int)&Elf64_Phdr_ARRAY_00010040[4].p_offset + iVar2 + 4),
               iVar2 + 0x10130);
  lVar10 = 0x1000;
  *(undefined4 *)(iVar2 + 0xa0) = 0xaf00000;
  *(undefined2 *)(iVar2 + 0x92) = 0x1000;
  pcVar9 = _opd_FUN_00046718 + iVar2 + 4;
  *(int *)(iVar2 + 0x78) = iVar2 + 0x46700;
  *(undefined2 *)(iVar2 + 0x80) = 0;
  *(undefined2 *)(iVar2 + 0x82) = 0xfff;
  *(undefined2 *)(iVar2 + 0x8e) = 0xffff;
  *(int *)(iVar2 + 0xb0) = iVar2 + 0x66700;
  *(undefined4 *)(iVar2 + 0x9c) = 0;
  *(undefined2 *)(iVar2 + 0x9a) = 0;
  *(undefined2 *)(iVar2 + 0x90) = 0;
  *(undefined2 *)(iVar2 + 0x94) = 0;
  *(undefined2 *)(iVar2 + 0x96) = 0;
  *(undefined2 *)(iVar2 + 0x98) = 0;
  *(undefined2 *)(iVar2 + 0x7c) = 0xffff;
  *(undefined2 *)(iVar2 + 0x7e) = 0xffff;
  *(undefined2 *)(iVar2 + 0x84) = 0xffff;
  *(undefined2 *)(iVar2 + 0x86) = 0xffff;
  *(undefined2 *)(iVar2 + 0x88) = 0xffff;
  *(undefined2 *)(iVar2 + 0x8a) = 0xffff;
  *(undefined2 *)(iVar2 + 0x8c) = 0xffff;
  iVar5 = 0;
  puVar7 = (undefined1 *)(iVar2 + 0x46708);
  do {
    iVar6 = iVar5 + 1;
    puVar7[2] = 0;
    *(short *)(pcVar9 + -0xc) = (short)iVar5 + -1;
    *(int *)pcVar9 = iVar6;
    *(short *)(pcVar9 + -10) = (short)iVar6;
    *(undefined2 *)(pcVar9 + -8) = 0;
    *puVar7 = 0;
    puVar7[1] = 1;
    pcVar9 = pcVar9 + 0x20;
    lVar10 = lVar10 + -1;
    iVar5 = iVar6;
    puVar7 = puVar7 + 0x20;
  } while (lVar10 != 0);
  *(undefined2 *)(iVar2 + 0x46710) = 0xffff;
  *(undefined2 *)(iVar2 + 0x666f2) = 0xffff;
  if (*(short *)(iVar2 + 0x9a) == 0) {
    iVar5 = *(int *)(iVar2 + 0x1c);
    *(int *)(iVar2 + 0x1c) = iVar5 + 0xaf00000;
    *(int *)(iVar2 + 0x24) = *(int *)(iVar2 + 0x24) + -0xaf00000;
    *(int *)(iVar2 + 0xa4) = iVar5 + *(int *)(iVar2 + 0x20);
    *(undefined4 *)(iVar2 + 0xac) = 0;
  }
  else {
    iVar5 = *(int *)(iVar2 + 0x28);
    *(int *)(iVar2 + 0x28) = iVar5 + 0xaf00000;
    *(int *)(iVar2 + 0x30) = *(int *)(iVar2 + 0x30) + -0xaf00000;
    *(int *)(iVar2 + 0xa4) = iVar5 + *(int *)(iVar2 + 0x2c);
    *(undefined4 *)(iVar2 + 0xac) = 1;
  }
  FUN_00fba95c(*(undefined4 *)(iVar2 + 0xa4),iVar2 + 0xa8);
  iVar5 = *(int *)(puVar4 + -0x7fec);
  iVar6 = *(int *)(iVar2 + 100);
  iVar3 = *(int *)(iVar2 + 0xa0);
  *(undefined4 *)(iVar5 + 0x60) = 0;
  iVar2 = *(int *)((int)&Elf64_Phdr_ARRAY_00010040[5].p_offset + iVar2 + 4);
  *(int *)(iVar5 + 8) = iVar6;
  *(undefined4 *)(iVar5 + 0x5c) = 0x1000;
  *(int *)(iVar5 + 0xc) = iVar3;
  *(int *)(iVar5 + 4) = iVar6 + iVar3 + iVar2 + *(int *)(iVar5 + 0x14);
  *(int *)(iVar5 + 0x10) = iVar2;
  *(undefined4 *)(iVar5 + 0x1c) = 0;
  *(undefined4 *)(iVar5 + 0x20) = 0;
  *(undefined4 *)(iVar5 + 0x24) = 0;
  *(undefined4 *)(iVar5 + 0x54) = 0x1000;
  *(undefined4 *)(iVar5 + 0x58) = 0;
  return;
}

