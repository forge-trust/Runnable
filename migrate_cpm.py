import os
import re

def remove_versions_from_csproj(root_dir):
    for dirpath, _, filenames in os.walk(root_dir):
        for filename in filenames:
            if filename.endswith('.csproj'):
                filepath = os.path.join(dirpath, filename)
                with open(filepath, 'r') as f:
                    content = f.read()
                
                def replace_tag(match):
                    tag_content = match.group(0)
                    new_tag = re.sub(r'\s+Version="[^"]*"', '', tag_content)
                    return new_tag

                new_content = re.sub(r'<PackageReference\s+[^>]*>', replace_tag, content)
                
                if new_content != content:
                    print(f"Updating {filepath}")
                    with open(filepath, 'w') as f:
                        f.write(new_content)

if __name__ == "__main__":
    remove_versions_from_csproj('/Users/andrew/Projects/forge-trust/runnable__venus')
